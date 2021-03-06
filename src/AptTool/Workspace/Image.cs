using System.Collections.Generic;
using AptTool.Apt;

namespace AptTool.Workspace
{
    public class Image
    {
        public bool ExcludeImportant { get; set; }
        
        public bool ExcludeRecommends { get; set; }
        
        public List<ImageAptRepo> Repositories { get; set; }
        
        public Dictionary<string, string> Packages { get; set; }
        
        public List<string> Preseeds { get; set; }
        
        public List<InstallScript> Scripts { get; set; }
    }
}