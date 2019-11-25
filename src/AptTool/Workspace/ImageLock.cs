using System.Collections.Generic;
using AptTool.Apt;

namespace AptTool.Workspace
{
    public class ImageLock
    {
        public Dictionary<string, AptVersion> InstalledPackages { get; set; }
    }
}