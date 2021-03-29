using System;
using System.Collections.Generic;
using System.IO;
using BindingAttributes;
using Microsoft.Extensions.Logging;

namespace AptTool.Apt.Impl
{
    [Binding(typeof(IAptDirectoryPrepService))]
    public class AptDirectoryPrepService : IAptDirectoryPrepService
    {
        private readonly ILogger<AptDirectoryPrepService> _logger;
        private bool _prepped;
        private string _aptDirectory;
        private string _aptConfigFile;
        
        public AptDirectoryPrepService(ILogger<AptDirectoryPrepService> logger)
        {
            _logger = logger;
        }
        
        public void Prep(string workspaceRoot, List<AptRepo> repositories, bool excludeRecommends)
        {
            if (repositories == null || repositories.Count == 0)
            {
                throw new Exception("You must provided at least one repository.");
            }

            var directory = Path.Combine(workspaceRoot, ".apt");
            
            _logger.LogInformation("Using apt directory: {aptDirectory}", directory);

            EnsureDirectory(directory);
            EnsureDirectory(Path.Combine(directory, "etc/apt/preferences.d"));
            EnsureDirectory(Path.Combine(directory, "etc/apt/apt.conf.d"));
            EnsureDirectory(Path.Combine(directory, "var/lib/dpkg"));
            EnsureFile(Path.Combine(directory, "var/lib/dpkg/status"));
            EnsureDirectory(Path.Combine(directory, "etc/apt/sources.list.d"));
            
            if (File.Exists(Path.Combine(directory, "etc/apt/sources.list")))
            {
                File.Delete(Path.Combine(directory, "etc/apt/sources.list"));
            }
            using (var stream = File.OpenWrite(Path.Combine(directory, "etc/apt/sources.list")))
            using (var writer = new StreamWriter(stream))
            {
                foreach (var repo in repositories)
                {
                    writer.WriteLine(repo);
                }
            }

            var aptConfig = Path.Combine(directory, "tmp-apt.conf");
            if (File.Exists(aptConfig))
            {
                File.Delete(aptConfig);
            }

            using (var stream = File.OpenWrite(aptConfig))
                using(var writer = new StreamWriter(stream))
            {
                writer.WriteLine($"Dir \"{directory}\";");
                // Use the local gpg keys.
                writer.WriteLine("Dir::Etc::Trusted \"/etc/apt/trusted.gpg\";");
                writer.WriteLine("Dir::Etc::TrustedParts \"/etc/apt/trusted.gpg.d\";");
                writer.WriteLine("Acquire::Check-Valid-Until \"false\";");
                writer.WriteLine($"APT::Install-Recommends \"{(!excludeRecommends).ToString().ToLower()}\";");
                // Don't download translations.
                writer.WriteLine("Acquire::Languages \"none\";");
            }

            _aptConfigFile = aptConfig;
            _aptDirectory = directory;
            _prepped = true;
        }

        public string AptDirectory
        {
            get
            {
                if (!_prepped)
                {
                    throw new Exception("Call IAptDirectoryPrepService.Prep() first.");
                }

                return _aptDirectory;
            }
        }

        public string AptConfigFile
        {
            get
            {
                if (!_prepped)
                {
                    throw new Exception("Call IAptDirectoryPrepService.Prep() first.");
                }

                return _aptConfigFile;
            }
        }

        private void EnsureDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private void EnsureFile(string file)
        {
            if (!File.Exists(file))
            {
                using (var stream = File.OpenWrite(file))
                {
                    
                }
            }
        }
    }
}