using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AptTool.Process;
using BindingAttributes;

namespace AptTool.Apt.Impl
{
    [Binding(typeof(IAptCacheService))]
    public class AptCacheService : IAptCacheService
    {
        private readonly IProcessRunner _processRunner;
        private readonly IAptDirectoryPrepService _aptDirectoryPrepService;

        public AptCacheService(IProcessRunner processRunner,
            IAptDirectoryPrepService aptDirectoryPrepService)
        {
            _processRunner = processRunner;
            _aptDirectoryPrepService = aptDirectoryPrepService;
        }

        public List<string> Packages()
        {
            var packages = _processRunner.ReadShell("apt-cache pkgnames", new RunnerOptions
            {
                Env = new Dictionary<string, string>
                {
                    { "APT_CONFIG", _aptDirectoryPrepService.AptConfigFile },
                    { "DEBIAN_FRONTEND", "noninteractive" },
                    { "DEBCONF_NONINTERACTIVE_SEEN", "true" },
                    { "LC_ALL", "C" },
                    { "LANGUAGE", "C" },
                    { "LANG", "C" }
                }
            });
            return packages.Split(Environment.NewLine).Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        public Dictionary<string, Dictionary<AptVersion, DebPackageInfo>> Show(Dictionary<string, AptVersion> packages)
        {
            if (packages.Count == 0)
            {
                throw new Exception("You must provide at least one package.");
            }

            var result = new Dictionary<string, Dictionary<AptVersion, DebPackageInfo>>();
            Chunk(packages.Keys, (chunk) =>
            {
                var output = _processRunner.ReadShell($"apt-cache show {string.Join(" ", chunk.Select(x => packages[x].ToCommandParameter(x)))}", new RunnerOptions
                {
                    Env = new Dictionary<string, string>
                    {
                        { "APT_CONFIG", _aptDirectoryPrepService.AptConfigFile },
                        { "DEBIAN_FRONTEND", "noninteractive" },
                        { "DEBCONF_NONINTERACTIVE_SEEN", "true" },
                        { "LC_ALL", "C" },
                        { "LANGUAGE", "C" },
                        { "LANG", "C" }
                    }
                });

                using (var streamReader = new StringReader(output))
                {
                    string debPackageVersion = null;
                    string debPackageArchitecture = null;
                    string debPackageSource = null;
                    string debPackageSourceVersion = null;
                    DebPackageInfo debPackageInfo = null;
                    string debPackageName = null;
                    
                    var line = streamReader.ReadLine();
                    while (line != null)
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            if (!string.IsNullOrEmpty(debPackageName))
                            {
                                if (string.IsNullOrEmpty(debPackageSource))
                                {
                                    debPackageSource = debPackageName;
                                }
                                if (string.IsNullOrEmpty(debPackageSourceVersion))
                                {
                                    debPackageSourceVersion = debPackageVersion;
                                }
                                debPackageInfo.SourcePackage = debPackageSource;
                                debPackageInfo.SourceVersion = debPackageSourceVersion;
                                
                                if (result.ContainsKey(debPackageName))
                                {
                                    result[debPackageName].Add(new AptVersion(debPackageVersion, debPackageArchitecture), debPackageInfo);
                                }
                                else
                                {
                                    result[debPackageName] = new Dictionary<AptVersion, DebPackageInfo>
                                    {
                                        { new AptVersion(debPackageVersion, debPackageArchitecture), debPackageInfo }
                                    };
                                }

                                debPackageName = null;
                                debPackageVersion = null;
                                debPackageArchitecture = null;
                                debPackageInfo = null;
                                debPackageSource = null;
                                debPackageSourceVersion = null;
                            }
                            line = streamReader.ReadLine();
                            continue;
                        }

                        if (line.StartsWith("Package: "))
                        {
                            debPackageName = line.Substring("Package: ".Length);
                            debPackageInfo = new DebPackageInfo();
                        }
                        else if (line.StartsWith("Version: "))
                        {
                            debPackageVersion = line.Substring("Version: ".Length);
                        }
                        else if (line.StartsWith("Priority: "))
                        {
                            debPackageInfo.Priority = line.Substring("Priority: ".Length);
                        }
                        else if (line.StartsWith("Essential: "))
                        {
                            debPackageInfo.Essential = line.Substring("Essential: ".Length);
                        }
                        else if (line.StartsWith("Architecture: "))
                        {
                            debPackageArchitecture = line.Substring("Architecture: ".Length);
                        }
                        else if (line.StartsWith("Source: "))
                        {
                            line = line.Substring("Source: ".Length);
                            var parsed = Regex.Match(line, @"^([a-zA-Z0-9.+-]+)(?:\s+\(([a-zA-Z0-9.+:~-]+)\))?$");
                            if (!parsed.Success)
                            {
                                throw new Exception($"Invalid source line: {line}");
                            }
                            debPackageSource = parsed.Groups[1].Value;
                            debPackageSourceVersion = parsed.Groups[2].Value;
                        }
                        else
                        {
                            // Not supported...
                        }

                        line = streamReader.ReadLine();
                    }

                    if (!string.IsNullOrEmpty(debPackageName))
                    {
                        if (string.IsNullOrEmpty(debPackageSource))
                        {
                            debPackageSource = debPackageName;
                        }
                        if (string.IsNullOrEmpty(debPackageSourceVersion))
                        {
                            debPackageSourceVersion = debPackageVersion;
                        }
                        debPackageInfo.SourcePackage = debPackageSource;
                        debPackageInfo.SourceVersion = debPackageSourceVersion;
                        
                        if (result.ContainsKey(debPackageName))
                        {
                            result[debPackageName].Add(new AptVersion(debPackageVersion, debPackageArchitecture),
                                debPackageInfo);
                        }
                        else
                        {
                            result[debPackageName] = new Dictionary<AptVersion, DebPackageInfo>
                            {
                                {new AptVersion(debPackageVersion, debPackageArchitecture), debPackageInfo}
                            };
                        }
                    }
                }
            });

            return result;
        }
        
        public List<string> ImportantAndEssentialPackages()
        {
            var result = new List<string>();
            
            // TODO: Only get package lists for architecture we care about.
            // Only needs to be done when we support cross-arch building of rootfs (not needed).
            foreach(var repoPackageCache in Directory.GetFiles(Path.Combine(_aptDirectoryPrepService.AptDirectory, "var", "lib", "apt", "lists"), "*binary-*_Packages"))
            {
                using (var file = File.OpenRead(repoPackageCache))
                using(var reader = new StreamReader(file))
                {
                    string packageName = null;
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        if (line.StartsWith("Package: "))
                        {
                            packageName = line.Substring("Package: ".Length).Trim();
                            continue;
                        }

                        if (line.StartsWith("Priority: "))
                        {
                            if (line.Substring("Priority: ".Length).Trim() == "important")
                            {
                                result.Add(packageName);
                            }
                            continue;
                        }

                        if (line.StartsWith("Essential: "))
                        {
                            if (line.Substring("Essential: ".Length).Trim() == "yes")
                            {
                                result.Add(packageName);
                            }
                        }
                    }
                }
            }

            return result.Distinct().ToList();
        }

        private void Chunk(IEnumerable<string> input, Action<List<string>> action)
        {
            var all = input.ToList();
            
            while (all.Count > 0)
            {
                var toTake = Math.Min(3000, all.Count);
                var current = all.GetRange(0, toTake);
                all.RemoveRange(0, toTake);

                action(current);
            }
        }
    }
}