using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                    DebPackageInfo debPackageInfo = null;
                    string debPackageName = null;
                    
                    var line = streamReader.ReadLine();
                    while (line != null)
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            if (!string.IsNullOrEmpty(debPackageName))
                            {
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
                        else if (line.StartsWith("Depends: "))
                        {
                            debPackageInfo.Dependencies = ParseDependencies(line.Substring("Depends: ".Length));
                        }
                        else if (line.StartsWith("Pre-Depends: "))
                        {
                            debPackageInfo.PreDependencies = ParseDependencies(line.Substring("Pre-Depends: ".Length));
                        }
                        else if (line.StartsWith("Provides: "))
                        {
                            debPackageInfo.Provides = ParseDependencies(line.Substring("Provides: ".Length));
                        }
                        else
                        {
                            // Not supported...
                        }

                        line = streamReader.ReadLine();
                    }

                    if (!string.IsNullOrEmpty(debPackageName))
                    {
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
        
        public Dictionary<string, PolicyInfo> Policy(List<string> packages)
        {
            if (packages.Count == 0)
            {
                throw new Exception("You must provide at least one package.");
            }

            var result = new Dictionary<string, PolicyInfo>();
            Chunk(packages, (chunk) =>
            {
                var output = _processRunner.ReadShell($"apt-cache policy {string.Join(" ", chunk)}", new RunnerOptions
                {
                    Env =  new Dictionary<string, string>
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
                    KeyValuePair<string, PolicyInfo>? policy = null;
                    
                    var line = streamReader.ReadLine();
                    while (line != null)
                    {
                        if (!line.StartsWith(" "))
                        {
                            if (policy.HasValue)
                            {
                                result.Add(policy.Value.Key, policy.Value.Value);
                                policy = null;
                            }
                            policy = new KeyValuePair<string, PolicyInfo>(line.Trim().TrimEnd(':'), new PolicyInfo());
                        }
                        else if (line.StartsWith("  Candidate:"))
                        {
                            policy.Value.Value.CandidateVersion = line.Substring("  Candidate:".Length).Trim();
                        }
                        
                        line = streamReader.ReadLine();
                    }

                    if (policy.HasValue)
                    {
                        result.Add(policy.Value.Key, policy.Value.Value);
                    }
                }
            });

            return result;
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

        private List<PackageDependency> ParseDependencies(string line)
        {
            var depends = line.Split(", ")
                .Select(x =>
                {
                    var firstIndex = x.IndexOf("(", StringComparison.Ordinal);
                    if (firstIndex != -1)
                    {
                        return x.Substring(0, firstIndex - 1);
                    }

                    return x;
                })
                .Select(x =>
                {
                    var firstIndex = x.IndexOf(":", StringComparison.Ordinal);
                    if (firstIndex != -1)
                    {
                        return x.Substring(0, firstIndex);
                    }

                    return x;
                })
                .Select(x => x.Trim())
                .Distinct()
                .ToList();
            return depends.Select(x =>
            {
                if (x.Contains("|"))
                {
                    return (PackageDependency)new PackageDependencyAlternates(ParseDependencies(x.Replace("|", ",")).Select(
                        y => ((PackageDependencySpecific) y).Package).ToList());
                }

                return (PackageDependency)new PackageDependencySpecific(x);
            }).ToList();
        }
    }
}