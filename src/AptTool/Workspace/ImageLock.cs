using System.Collections.Generic;
using AptTool.Apt;

namespace AptTool.Workspace
{
    public class ImageLock
    {
        public Dictionary<string, PackageEntry> InstalledPackages { get; set; }

        public class PackageEntry
        {
            public AptVersion Version { get; set; }
            
            public PackageSource Source { get; set; }
        }

        public class PackageSource
        {
            public string Name { get; set; }
            
            public string Version { get; set; }
        }
    }
}