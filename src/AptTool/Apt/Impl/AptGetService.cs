using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AptTool.Process;
using BindingAttributes;

namespace AptTool.Apt.Impl
{
    [Binding(typeof(IAptGetService))]
    public class AptGetService : IAptGetService
    {
        private readonly IAptDirectoryPrepService _aptDirectoryPrepService;
        private readonly IProcessRunner _processRunner;

        public AptGetService(IAptDirectoryPrepService aptDirectoryPrepService,
            IProcessRunner processRunner)
        {
            _aptDirectoryPrepService = aptDirectoryPrepService;
            _processRunner = processRunner;
        }
        
        public void Update()
        {
            _processRunner.RunShell("apt-get update", new RunnerOptions
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
        }

        public void Download(Dictionary<string, AptVersion> packages, string directory = null)
        {
            Chunk(packages.Keys, chunk =>
            {
                _processRunner.RunShell($"apt-get download {string.Join(" ", chunk.Select(x => packages[x].ToCommandParameter(x)))}", new RunnerOptions
                {
                    WorkingDirectory = directory,
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
            });
        }

        public Dictionary<string, AptVersion> SimulateInstall(Dictionary<string, AptVersion> packages)
        {
            var result = new Dictionary<string, AptVersion>();
            
            var output = _processRunner.ReadShell($"apt-get install -s -y {string.Join(" ", packages.Select(x => x.Value.ToCommandParameter(x.Key)))}", new RunnerOptions
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
                var line = streamReader.ReadLine();
                while (line != null)
                {
                    var match = Regex.Match(line, @"Inst (\S*) \((\S*)(.*)\[(\S*)\]\)");
                    if (match.Success)
                    {
                        var packageName = match.Groups[1].Captures[0].Value;
                        var version = match.Groups[2].Captures[0].Value;
                        var architecture = match.Groups[4].Captures[0].Value;
                        result.Add(packageName, new AptVersion(version, architecture));
                    }

                    line = streamReader.ReadLine();
                }
            }

            return result;
        }

        private void Chunk(IEnumerable<string> input, Action<List<string>> action)
        {
            var all = input.ToList();
            
            while (all.Count > 0)
            {
                var toTake = Math.Min(5000, all.Count);
                var current = all.GetRange(0, toTake);
                all.RemoveRange(0, toTake);

                action(current);
            }
        }
    }
}