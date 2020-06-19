using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AptTool.Apt;
using AptTool.Process;
using AptTool.Process.Impl;
using AptTool.Security;
using BindingAttributes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using ServiceStack;

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

        public Workspace(IAptDirectoryPrepService aptDirectoryPrepService, Microsoft.Extensions.Options.IOptions<WorkspaceConfig> workspaceConfig,
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

        public Image GetImage()
        {
            var imageFile = Path.Combine(_workspaceConfig.RootDirectory, "image.json");
            if (!File.Exists(imageFile))
            {
                throw new Exception("The file image.json doesn't exist.");
            }

            var result = JsonConvert.DeserializeObject<Image>(File.ReadAllText(imageFile), _jsonSerializerSettings);

            if (result.Repositories == null || result.Repositories.Count == 0)
            {
                throw new Exception("You must provide at least one repository.");
            }

            return result;
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
  
            _logger.LogInformation("Looking for all the essential/required/important packages...");
            var packages = new Dictionary<string, AptVersion>();
            foreach (var importantPackage in _aptCacheService.ImportantAndEssentialPackages())
            {
                packages[importantPackage] = AptVersion.Unspecified;
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
            var packageInfos = _aptCacheService.Show(packagesToInstall);
            
            var packageEntries = packagesToInstall.ToDictionary(x => x.Key, x => new ImageLock.PackageEntry
                {
                    Version = x.Value,
                    Source = new ImageLock.PackageSource
                    {
                        Name = packageInfos[x.Key][x.Value].SourcePackage,
                        Version = packageInfos[x.Key][x.Value].SourceVersion
                    }
                });
            
            _logger.LogInformation("Resolved packages:");
            foreach (var packageToInstall in packageEntries)
            {
                _logger.LogInformation($"\t{packageToInstall.Value.Version.ToCommandParameter(packageToInstall.Key)}");
            }
            
            _logger.LogInformation("Saving image-lock.json...");
            var lockFile = Path.Combine(_workspaceConfig.RootDirectory, "image-lock.json");
            if (File.Exists(lockFile))
            {
                File.Delete(lockFile);
            }
            File.WriteAllText(lockFile, JsonConvert.SerializeObject(new ImageLock
            {
                InstalledPackages = packageEntries
            }, _jsonSerializerSettings));
            
            _logger.LogInformation("Done!");
        }

        public void GenerateRootFs(string directory, bool overwrite, bool runStage2)
        {
            if (!Env.IsRoot)
            {
                _logger.LogWarning("The currently running user is not root. Some commands will be run with sudo.");
            }

            _logger.LogInformation("Updating the package cache...");
            _aptGetService.Update();
            
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

            var preseedFiles = GetPreseeds(image);
            var scripts = GetScripts(image);
            
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
            _processRunner.RunShell("touch \"var/lib/dpkg/available\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });
            _processRunner.RunShell("touch \"var/lib/dpkg/status\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });
            _processRunner.RunShell("mkdir -p \"var/lib/dpkg/updates\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });
            _processRunner.RunShell("mkdir -p \"etc/apt\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });
            _processRunner.RunShell("mkdir -p \"etc/apt\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });
            _processRunner.RunShell("mkdir -p \"stage2/debs\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });
            _processRunner.RunShell("mkdir -p \"stage2/preseeds\"", new RunnerOptions { UseSudo = !Env.IsRoot, WorkingDirectory = directory });

            _logger.LogInformation("Including apt repositories...");
            foreach (var repo in GetRepositories(image))
            {
                _processRunner.RunShell($"echo {repo.ToString().Quoted()} | tee -a ./etc/apt/sources.list",
                    new RunnerOptions {UseSudo = !Env.IsRoot, WorkingDirectory = directory});
            }
            
            // Download the packages.
            _logger.LogInformation("Downloading the debs...");
            var debFolder = Path.Combine(_workspaceConfig.RootDirectory, ".debs");
            debFolder.EnsureDirectoryExists();
            _aptGetService.Download(imageLock.InstalledPackages.ToDictionary(x => x.Key, x => x.Value.Version), debFolder);

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
            stage2Script.AppendLine($"export APT_TOOL_ROOTFS_NATIVE_DIR={directory.Quoted()}");
            
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
                var debFile = Path.Combine(debFolder, $"{package}_{installedPackage.Version.Version.Replace(":", "%3a")}_{installedPackage.Version.Architecture}.deb");
                if (!File.Exists(debFile))
                {
                    throw new Exception($"The deb file {debFile} doesn't exist.");
                }
                
                _logger.LogInformation("Extracting: {package}", imageLock.InstalledPackages[package].Version.ToCommandParameter(package));
                
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

            if (scripts.Count >= 0)
            {
                _logger.LogInformation("Including the custom scripts in the stage2...");
                var destinationDirectory = Path.Combine(directory, "stage2", "scripts");
                _processRunner.RunShell($"mkdir -p {destinationDirectory.Quoted()}", new RunnerOptions{ UseSudo = !Env.IsRoot });

                foreach (var scriptLookup in scripts.ToLookup(x => x.Directory))
                {
                    var destinationScriptDirectoryName = $"{Path.GetFileName(scriptLookup.Key)}-{Guid.NewGuid().ToString().Replace("-", "")}";
                    var destinationScriptDirectory = Path.Combine(destinationDirectory, destinationScriptDirectoryName);
                    _processRunner.RunShell($"mkdir {destinationScriptDirectory}", new RunnerOptions{ UseSudo = !Env.IsRoot });
                    _processRunner.RunShell($"cp -ra {scriptLookup.Key}/* {destinationScriptDirectory}/", new RunnerOptions{ UseSudo = !Env.IsRoot });

                    foreach (var script in scriptLookup)
                    {
                        stage2Script.AppendLine(
                            $"bash -c \"cd /stage2/scripts/{destinationScriptDirectoryName} && ./{script.Name}\"");
                    }
                }
            }
            
            if (!runStage2) // Done put this message if we will be deleting this ourselves.
            {
                stage2Script.AppendLine("echo \"DONE! Don't forget to delete /stage2!!\"");
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

        private List<InstallScript> GetScripts(Image image)
        {
            return (image.Scripts ?? new List<InstallScript>()).Select(x =>
            {
                if (string.IsNullOrEmpty(x.Name))
                {
                    throw new Exception("You must provide a script name.");
                }

                if (string.IsNullOrEmpty(x.Directory))
                {
                    throw new Exception("You must provide a script directory.");
                }

                _logger.LogInformation("Including script: {@script}", x);

                var path = x.Directory;
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(_workspaceConfig.RootDirectory, path);
                }

                path = Path.GetFullPath(path);
                if (!Directory.Exists(path))
                {
                    throw new Exception($"The script directory {x.Directory} doesn't exist.");
                }

                x.Directory = path;
                
                var scriptPath = $"{x.Directory}{Path.DirectorySeparatorChar}{x.Name}";
                if (!File.Exists(scriptPath))
                {
                    throw new Exception($"The install script {x.Name} doesn't exist.");
                }
                
                return x;
            }).ToList();
        }

        private List<string> GetPreseeds(Image image)
        {
            return (image.Preseeds ?? new List<string>()).Select(x =>
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
        }

        private List<AptRepo> GetRepositories(Image image = null)
        {
            if (image == null)
            {
                image = GetImage();
            }

            return image.Repositories.SelectMany(x =>
            {
                var result = new List<AptRepo> {x};
                if (x.IncludeSourcePackages && !x.Source)
                {
                    result.Add(new AptRepo(x.Trusted, x.Uri, x.Distribution, true, x.Components.ToArray()));
                }

                return result;
            }).Select(x =>
            {
                // Let's detect if we have a relative path to a local repo.
                // if we detect "file:../somewhere", we need to resolve that path, relative to
                // the images.json files.
                if (x.Uri.StartsWith("file:"))
                {
                    var path = x.Uri.Substring("file:".Length);
                    if (Path.IsPathRooted(path))
                    {
                        // Absolute paths don't need to be changed.
                        return x;
                    }

                    var absolutePath = _processRunner.ReadShell($"readlink -f {path}", new RunnerOptions
                    {
                        WorkingDirectory = _workspaceConfig.RootDirectory
                    }).TrimEnd(Environment.NewLine.ToArray());
                    if (string.IsNullOrEmpty(absolutePath))
                    {
                        throw new Exception($"Unknown file path for apt repo: {path}");
                    }
                    return new AptRepo(x.Trusted, $"file:{absolutePath}", x.Distribution, x.Source, x.Components.ToArray());
                }
                return x;
            }).ToList();
        }

        public class ChangelogEntry
        {
            public string SourcePackage { get; set; }
            
            public string MD5 { get; set; }
        }
        
        public void  SyncChangelogs()
        {
            var imageLock = GetImageLock();
            
            _logger.LogInformation("Updating the package cache...");
            _aptGetService.Update();
            
            // Download the packages.
            _logger.LogInformation("Downloading the debs...");
            var debFolder = Path.Combine(_workspaceConfig.RootDirectory, ".debs");
            debFolder.EnsureDirectoryExists();
            _aptGetService.Download(imageLock.InstalledPackages.ToDictionary(x => x.Key, x => x.Value.Version), debFolder);
            
            var tmpChangelogDirectory = Path.Combine(_workspaceConfig.RootDirectory, ".tmp-changelog");
            tmpChangelogDirectory.CleanOrCreateDirectory();
            
            var tmpExtractionDirectory = Path.Combine(tmpChangelogDirectory, "tmp");
            Directory.CreateDirectory(tmpExtractionDirectory);
            
            foreach (var package in imageLock.InstalledPackages.Keys)
            {
                var installedPackage = imageLock.InstalledPackages[package];
                var debFile = Path.Combine(debFolder, $"{package}_{installedPackage.Version.Version.Replace(":", "%3a")}_{installedPackage.Version.Architecture}.deb");
                if (!File.Exists(debFile))
                {
                    throw new Exception($"The deb file {debFile} doesn't exist.");
                }
                
                _logger.LogInformation("Extracting: {package}", imageLock.InstalledPackages[package].Version.ToCommandParameter(package));
                
                _dpkgService.Extract(debFile, tmpExtractionDirectory, false);
            }
            
            _logger.LogInformation("Finding changelogs...");
            var docsDirectory = Path.Combine(tmpExtractionDirectory, "usr", "share", "doc");
            var changelogEntries = new List<ChangelogEntry>();
            foreach (var packageDirectoryPath in Directory.GetDirectories(docsDirectory))
            {
                var packageDirectoryName = Path.GetFileName(packageDirectoryPath);
                
                _logger.LogInformation("Finding changelog for {package}...", packageDirectoryName);
                
                string FindChangelog(string baseName)
                {
                    var debianName = baseName + ".Debian";
                    if (File.Exists(debianName))
                    {
                        return debianName;
                    }
                    debianName += ".gz";
                    if (File.Exists(debianName))
                    {
                        return debianName;
                    }
                    if (File.Exists(baseName))
                    {
                        return baseName;
                    }
                    baseName += ".gz";
                    if (File.Exists(baseName))
                    {
                        return baseName;
                    }
                    return null;
                }
                
                var changelogPath = FindChangelog(Path.Combine(packageDirectoryPath, "changelog"));
                if (string.IsNullOrEmpty(changelogPath))
                {
                    _logger.LogWarning("Couldn't find changelog in {package} doc directory.", packageDirectoryName);
                    continue;
                }

                var changelogEntry = new ChangelogEntry();
                changelogEntry.MD5 = _processRunner.ReadShell($"md5sum {changelogPath.Quoted()}");
                changelogEntry.MD5 = changelogEntry.MD5.Substring(0, changelogEntry.MD5.IndexOf(" ", StringComparison.Ordinal));
                changelogEntry.SourcePackage = _processRunner.ReadShell($"dpkg-parsechangelog -S Source -l {changelogPath.Quoted()}").TrimEnd(Environment.NewLine.ToCharArray());

                var existing = changelogEntries.SingleOrDefault(x =>
                    x.MD5 == changelogEntry.MD5 && x.SourcePackage == changelogEntry.SourcePackage);
                if (existing != null)
                {
                    _logger.LogWarning("Changelog already exists for {packge} via {sourcePackage}", packageDirectoryName, changelogEntry.SourcePackage);
                    continue;
                }
                
                var destination = Path.Combine(tmpChangelogDirectory, changelogEntry.SourcePackage);
                _processRunner.RunShell($"dpkg-parsechangelog -S Changes --all -l {changelogPath.Quoted()} > {destination.Quoted()}");
                
                _logger.LogInformation("Saved changelog for {package}.", packageDirectoryName);
            }

            if (Directory.Exists(tmpExtractionDirectory))
            {
                Directory.Delete(tmpExtractionDirectory, true);
            }
            var changelogDirectory = Path.Combine(_workspaceConfig.RootDirectory, "changelogs");
            if (Directory.Exists(changelogDirectory))
            {
                Directory.Delete(changelogDirectory, true);
            }
            Directory.Move(tmpChangelogDirectory, changelogDirectory);
            
            _logger.LogInformation("Done!");
        }

        public void SaveAuditReport(string suite, string database)
        {
            bool VersionLessThan(string version1, string version2)
            {
                try
                {
                    _processRunner.RunShell($"dpkg --compare-versions \"{version1}\" \"lt\" \"{version2}\"", new RunnerOptions
                    {
                        LogCommandOnError = false
                    });
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            
            // Make sure dpkg --compare-versions works
            if (!VersionLessThan("1", "2") || VersionLessThan("2", "1"))
            {
                throw new Exception("dpkg --compare-versions appears to not be working.");
            }
            
            var securityDb = new SecurityDb(database);
            
            var imageLock = GetImageLock();

            var auditReport = new AuditReport();
            
            foreach (var package in imageLock.InstalledPackages)
            {
                var sourcePackage = package.Value.Source;
                if (sourcePackage == null || sourcePackage.Name.IsNullOrEmpty() ||
                    sourcePackage.Version.IsNullOrEmpty())
                {
                    continue;
                }

                // Here is a description of how these issues are associated with versions/releases.
                // 1. If a package note has no release, it means that package note applies to all versions.
                // 2. If that package note has a fixed version of "0", it means that it isn't affected.
                // 3. If the package note has a fixed version of null, it means that it is affected (or hasn't been determined yet).
                // Then, if there is a package note for a specific release, it overrides 1-3 on a per-suite level.
                var bugNames = securityDb.GetPackageNotesForPackage(sourcePackage.Name).Select(x => x.BugName).Distinct().ToList();

                foreach (var bugName in bugNames)
                {
                    var packageNote = securityDb.GetPackageNoteForBugInSuite(sourcePackage.Name, bugName, suite);
                    if (packageNote == null)
                    {
                        packageNote = securityDb.GetPackageNoteForBugInAllSuites(sourcePackage.Name, bugName);
                    }

                    if (packageNote == null)
                    {
                        continue;
                    }
                    
                    if (packageNote.FixedVersion == "0")
                    {
                        // Explicitly marked as "not affected!"
                        continue;
                    }

                    void TrackBug()
                    {
                        var auditSourcePackage = auditReport.Sources.SingleOrDefault(x =>
                            x.Name == sourcePackage.Name && x.Version == sourcePackage.Version);
                        if (auditSourcePackage == null)
                        {
                            auditSourcePackage = new AuditReport.AuditSourcePackage
                            {
                                Name = sourcePackage.Name,
                                Version = sourcePackage.Version
                            };
                            auditReport.Sources.Add(auditSourcePackage);
                        }
                        auditSourcePackage.Binaries[package.Key] = package.Value.Version;

                        if (auditSourcePackage.Vulnerabilities.Any(x => x.Name == bugName))
                        {
                            return;
                        }
                        
                        var bug = securityDb.GetBug(packageNote.BugName);
                        var nvdData = securityDb.GetNvdData(bug.Name);

                        var vulnerability = new AuditReport.Vulnerability
                        {
                            Name = packageNote.BugName,
                            Description = bug.Description,
                            Severity = packageNote.Urgency
                        };

                        if (nvdData != null)
                        {
                            vulnerability.Description = nvdData.CveDescription;
                            vulnerability.NvdSeverity = nvdData.Severity;
                        }

                        if (vulnerability.Name.StartsWith("DSA") || vulnerability.Name.StartsWith("DLA"))
                        {
                            var references = securityDb.GetReferences(bug.Name);
                            if (references.Count > 0)
                            {
                                vulnerability.References = references;
                            }
                        }

                        vulnerability.FixedVersion = packageNote.FixedVersion;
                        if (string.IsNullOrEmpty(vulnerability.FixedVersion))
                        {
                            vulnerability.FixedVersion = "NONE";
                        }

                        var noDsa = securityDb.GetNoDsaInfoForPackage(sourcePackage.Name, bugName, suite);
                        if (noDsa != null)
                        {
                            vulnerability.NoDsa = new AuditReport.NoDsa
                            {
                                Comment = string.IsNullOrEmpty(noDsa.Comment) ? null : noDsa.Comment,
                                Reason = string.IsNullOrEmpty(noDsa.Reason) ? null : noDsa.Reason
                            };
                        }

                        var bugNotes = securityDb.GetBugNotes(bug.Name);

                        var notes = new List<string>();
                        var packageRegex = new Regex(@"^(?:\[([a-z]+)\]\s)?-\s([A-Za-z0-9:.+-]+)\s+<([a-z-]+)>\s*(?:\s\((.*)\))?$", RegexOptions.Compiled);
                        foreach (var bugNote in bugNotes)
                        {
                            var match = packageRegex.Match(bugNote.Comment);
                            if (match.Success)
                            {
                                continue;
                            }
                            
                            notes.Add(bugNote.Comment);
                        }

                        if (notes.Count > 0)
                        {
                            vulnerability.Notes = notes;
                        }
                        
                        auditSourcePackage.Vulnerabilities.Add(vulnerability);
                    }

                    if (string.IsNullOrEmpty(packageNote.FixedVersion))
                    {
                        // Marked as affected, with no fixed version.
                        TrackBug();
                    }
                    else
                    {
                        // Has a fixed version.
                        if (VersionLessThan(package.Value.Version.Version, packageNote.FixedVersion))
                        {
                            // And we don't have it!
                            TrackBug();
                        }
                    }
                }
            }

            var file = Path.Combine(_workspaceConfig.RootDirectory, "debian-audit.json");
            if (File.Exists(file))
            {
                File.Delete(file);
            }
            File.WriteAllText(file, JsonConvert.SerializeObject(auditReport, _jsonSerializerSettings));
        }
    }
}