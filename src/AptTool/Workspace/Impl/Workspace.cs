using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using AptTool.Apt;
using AptTool.Process;
using BindingAttributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AptTool.Workspace.Impl
{
    [Binding(typeof(IWorkspace))]
    public class Workspace : IWorkspace
    {
        private readonly IAptDirectoryPrepService _aptDirectoryPrepService;
        private readonly IAptCacheService _aptCacheService;
        private readonly ILogger<Workspace> _logger;
        private readonly IAptGetService _aptGetService;
        private readonly IDpkgService _dpkgService;
        private readonly IProcessRunner _processRunner;
        private readonly WorkspaceConfig _workspaceConfig;
        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.Indented,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        public Workspace(IAptDirectoryPrepService aptDirectoryPrepService,
            IOptions<WorkspaceConfig> workspaceConfig,
            IAptCacheService aptCacheService,
            ILogger<Workspace> logger,
            IAptGetService aptGetService,
            IDpkgService dpkgService,
            IProcessRunner processRunner)
        {
            _aptDirectoryPrepService = aptDirectoryPrepService;
            _aptCacheService = aptCacheService;
            _logger = logger;
            _aptGetService = aptGetService;
            _dpkgService = dpkgService;
            _processRunner = processRunner;
            _workspaceConfig = workspaceConfig.Value;
        }
        
        public void Init()
        {
            _aptDirectoryPrepService.Prep(_workspaceConfig.RootDirectory, GetRepositories());
        }

        public List<AptRepo> GetRepositories()
        {
            var file = Path.Combine(_workspaceConfig.RootDirectory, "repositories.json");
            if (!File.Exists(file))
            {
                throw new Exception("The file repositories.json doesn't exist.");
            }

            return JsonConvert.DeserializeObject<List<AptRepo>>(File.ReadAllText(file), _jsonSerializerSettings);
        }
        
        public Image GetImage()
        {
            var imageFile = Path.Combine(_workspaceConfig.RootDirectory, "image.json");
            if (!File.Exists(imageFile))
            {
                throw new Exception("The file image.json doesn't exist.");
            }

            return JsonConvert.DeserializeObject<Image>(File.ReadAllText(imageFile), _jsonSerializerSettings);
        }

        public ImageLock GetImageLock()
        {
            var imageFile = Path.Combine(_workspaceConfig.RootDirectory, "image-lock.json");
            if (!File.Exists(imageFile))
            {
                throw new Exception("The file image-lock.json doesn't exist.");
            }

            return JsonConvert.DeserializeObject<ImageLock>(File.ReadAllText(imageFile), _jsonSerializerSettings);
        }
        
        public void Install()
        {
            _logger.LogInformation("Updating the package cache...");
            _aptGetService.Update();
            
            var image = GetImage();
  
            _logger.LogInformation("Getting all package names...");
            var packageNames = _aptCacheService.Packages();

            _logger.LogInformation("Looking for all the essential/required/important packages...");
            var policies = _aptCacheService.Policy(packageNames);
            var packageInfos = _aptCacheService.Show(packageNames.ToDictionary(x => x,
                    x => new AptVersion(policies[x].CandidateVersion, null)));
            
            var packages = new Dictionary<string, AptVersion>();
            foreach (var packageName in packageNames)
            {
                var policy = policies[packageName];
                var packageInfo = packageInfos[packageName].Single(x => x.Key.Version == policy.CandidateVersion).Value;

                if (string.IsNullOrEmpty(packageInfo.Priority) && !string.IsNullOrEmpty(packageInfo.Essential) && packageInfo.Essential == "yes")
                {
                    packages.Add(packageName, new AptVersion(null, null));
                    continue;
                }

                if (packageInfo.Priority == "required")
                {
                    _logger.LogInformation("Adding required package: {package}", packageName);
                    packages.Add(packageName, AptVersion.Unspecified);
                } else if (packageInfo.Priority == "important")
                {
                    if (!image.ExcludeImportant)
                    {
                        _logger.LogInformation("Adding important package: {package}", packageName);
                        packages.Add(packageName, AptVersion.Unspecified);
                    }
                }
            }

            if (image.Packages != null)
            {
                foreach (var package in image.Packages.Keys)
                {
                    if (string.IsNullOrEmpty(image.Packages[package]))
                    {
                        throw new Exception("Empty version field detected.");
                    }
                    
                    if (image.Packages[package] == "latest")
                    {
                        if (packages.ContainsKey(package))
                        {
                            packages[package] = AptVersion.Unspecified;
                        }
                        else
                        {
                            packages.Add(package, AptVersion.Unspecified);
                        }
                    }
                    else
                    {
                        if (packages.ContainsKey(package))
                        {
                            packages[package] = new AptVersion(image.Packages[package], null);
                        }
                        else
                        {
                            packages.Add(package, new AptVersion(image.Packages[package], null));
                        }
                    }
                }
            }
            
            _logger.LogInformation("Declared packages:");
            foreach (var package in packages)
            {
                if (package.Value.Equals(AptVersion.Unspecified))
                {
                    _logger.LogInformation($"\t{package.Key}:latest", package.Key, "latest");
                }
                else
                {
                    _logger.LogInformation($"\t{package.Value.ToCommandParameter(package.Key)}");
                }
            }
            
            _logger.LogInformation("Calculating all the packages that need to be installed...");
            var packagesToInstall = _aptGetService.SimulateInstall(packages);
            
            _logger.LogInformation("Resolved packages:");
            foreach (var packageToInstall in packagesToInstall)
            {
                _logger.LogInformation($"\t{packageToInstall.Value.ToCommandParameter(packageToInstall.Key)}");
            }
            
            _logger.LogInformation("Saving image-lock.json...");
            var lockFile = Path.Combine(_workspaceConfig.RootDirectory, "image-lock.json");
            if (File.Exists(lockFile))
            {
                File.Delete(lockFile);
            }
            File.WriteAllText(lockFile, JsonConvert.SerializeObject(new ImageLock
            {
                InstalledPackages = packagesToInstall
            }, _jsonSerializerSettings));
            
            _logger.LogInformation("Done!");
        }

        public void GenerateRootFs(string directory, bool overwrite, bool runStage2)
        {
            if (!Env.IsRoot)
            {
                _logger.LogWarning("The currently running user is not root. Some commands will be run with sudo.");
            }

            if (runStage2)
            {
                // Let's make sure arch-chroot is available.
                if (!File.Exists("/usr/bin/arch-chroot"))
                {
                    _logger.LogError($"You indicated you wanted to run stage2, but arch-chroot isn't available. Try running {"sudo apt-get install arch-install-scripts".Quoted()}.");
                    throw new Exception("The command arch-chroot isn't available.");
                }
            }

            var image = GetImage();
            var imageLock = GetImageLock();

            var preseedFiles = (image.Preseeds ?? new List<string>()).Select(x =>
            {
                _logger.LogInformation("Including preseed file: {file}", x);
                var path = x;
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(_workspaceConfig.RootDirectory, path);
                }

                path = Path.GetFullPath(path);
                if (!File.Exists(path))
                {
                    throw new Exception($"The preseed file {x} doesn.t exist.");
                }

                return path;
            }).ToList();
            
            if (string.IsNullOrEmpty(directory))
            {
                directory = "rootfs";
            }

            if (!Path.IsPathRooted(directory))
            {
                directory = Path.Combine(Directory.GetCurrentDirectory(), directory);
            }

            directory = Path.GetFullPath(directory);
            
            _logger.LogInformation("Generating rootfs at {directory}.", directory);
            
            _logger.LogInformation("Cleaning directory...");
            if (overwrite)
            {
                if (Directory.Exists(directory))
                {
                    _logger.LogInformation("Directory exists, removing...");
                    _processRunner.RunShell($"rm -rf {directory.Quoted()}", new RunnerOptions{ UseSudo = !Env.IsRoot });
                }
                _processRunner.RunShell($"mkdir {directory.Quoted()}", new RunnerOptions{ UseSudo = !Env.IsRoot });
            }
            else
            {
                if (!Directory.Exists(directory))
                {
                    _logger.LogInformation("Creating directory..");
                    _processRunner.RunShell($"mkdir {directory.Quoted()}", new RunnerOptions{ UseSudo = !Env.IsRoot });
                }
                else
                {
                    // Make sure it is empty.
                    if (Directory.GetFiles(directory).Length > 0 || Directory.GetDirectories(directory, "*").Length > 0)
                    {
                        throw new Exception("The directory is not empty.");
                    }
                }
            }
            
            _logger.LogInformation("Creating required folders/files...");
            _processRunner.RunShell("mkdir -p \"var/lib/dpkg/info\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory});
            _processRunner.RunShell("touch \"var/lib/dpkg/status\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });
            _processRunner.RunShell("mkdir -p \"var/lib/dpkg/updates\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });
            _processRunner.RunShell("mkdir -p \"etc/apt\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });
            _processRunner.RunShell("mkdir -p \"etc/apt\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });
            _processRunner.RunShell("mkdir -p \"stage2/debs\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });
            _processRunner.RunShell("mkdir -p \"stage2/preseeds\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });

            _logger.LogInformation("Including apt repositories...");
            foreach (var line in File.ReadLines(Path.Combine(_aptDirectoryPrepService.AptDirectory,
                "etc/apt/sources.list")))
            {
                _processRunner.RunShell($"echo \"{line}\" | tee -a ./etc/apt/sources.list", new RunnerOptions{UseSudo = !Env.IsRoot, WorkingDirectory = directory});
            }
            
            // Download the packages.
            _logger.LogInformation("Downloading the debs...");
            var debFolder = Path.Combine(_workspaceConfig.RootDirectory, ".debs");
            debFolder.EnsureDirectoryExists();
            _aptGetService.Download(imageLock.InstalledPackages, debFolder);

            // Debs will go in here to reinstall in stage 2.
            var stage2DebDirectory = Path.Combine(directory, "stage2", "debs");
            
            var stage2Script = new StringBuilder();
            stage2Script.AppendLine("#!/bin/sh");
            stage2Script.AppendLine("set -e");
            stage2Script.AppendLine("export DEBIAN_FRONTEND=noninteractive");
            stage2Script.AppendLine("export DEBCONF_NONINTERACTIVE_SEEN=true ");
            stage2Script.AppendLine("export LC_ALL=C");
            stage2Script.AppendLine("export LANGUAGE=C");
            stage2Script.AppendLine("export LANG=C");
            
            // Step 1, run all the preseeds.
            foreach (var preseedFile in preseedFiles)
            {
                var fileName = Path.GetFileName(preseedFile) + Guid.NewGuid().ToString().Replace("-", "");
                _processRunner.RunShell($"cp {preseedFile.Quoted()} {$"stage2/preseeds/{fileName}".Quoted()}", new RunnerOptions{ UseSudo = !Env.IsRoot, WorkingDirectory = directory });
                stage2Script.AppendLine($"debconf-set-selections {$"/stage2/preseeds/{fileName}".Quoted()}");
            }
            
            foreach (var package in imageLock.InstalledPackages.Keys)
            {
                var installedPackage = imageLock.InstalledPackages[package];
                var debFile = Path.Combine(debFolder, $"{package}_{installedPackage.Version.Replace(":", "%3a")}_{installedPackage.Architecture}.deb");
                if (!File.Exists(debFile))
                {
                    throw new Exception($"The deb file {debFile} doesn't exist.");
                }
                
                _logger.LogInformation("Extracting: {package}", imageLock.InstalledPackages[package].ToCommandParameter(package));
                
                // Step 2, we extract the entire contents of the of the deb package.
                // This won't actually configure the package as installed, but it
                // will at least allow out chroot to execute commands (dpkg, etc).
                _dpkgService.Extract(debFile, directory, !Env.IsRoot);

                // Step 3, move the deb file into the rootfs for a stage2 setup.
                // In here, we will simply unpack the dpkg file, but not configure
                // it (only execute preinst).
                _processRunner.RunShell($"cp \"{debFile}\" \"{stage2DebDirectory}\"", new RunnerOptions{ UseSudo = !Env.IsRoot });
                stage2Script.AppendLine($"dpkg --unpack --force-confnew  --force-overwrite --force-depends /stage2/debs/{Path.GetFileName(debFile)}");
            }
            
            // Step 4, within the stage2, now we configure all the packages that we have unpacked.
            stage2Script.AppendLine("dpkg --configure -a");

            if (!runStage2) // Done put this message if we will be deleting this ourselves.
            {
                stage2Script.Append("echo \"DONE! Don't forget to delete /stage2!!\"");
            }

            _logger.LogInformation("Saving stage2 script to be run via chroot: {script}", "/stage2/stage2.sh");
            
            var stage2ScriptPath = Path.Combine(directory, "stage2", "stage2.sh");
            var tempPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempPath, stage2Script.ToString());
                _processRunner.RunShell($"cp \"{tempPath}\" \"{stage2ScriptPath}\"", new RunnerOptions{ UseSudo = !Env.IsRoot });
            }
            finally
            {
                File.Delete(tempPath);
            }
            
            _processRunner.RunShell($"chmod +x {stage2ScriptPath}", new RunnerOptions{ UseSudo = !Env.IsRoot });

            if (runStage2)
            {
                _logger.LogInformation("Running stage2...");
                _processRunner.RunShell($"arch-chroot {directory.Quoted()} /stage2/stage2.sh", new RunnerOptions{ UseSudo = !Env.IsRoot });
                _processRunner.RunShell($"rm -r {Path.Combine(directory, "stage2").Quoted()}", new RunnerOptions{ UseSudo = !Env.IsRoot });
            }
            
            _logger.LogInformation("Done!");
        }
    }
}