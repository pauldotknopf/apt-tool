using System.Collections.Generic;

namespace AptTool.Workspace
{
    public class Image
    {
        public bool ExcludeImportant { get; set; }
        
        public Dictionary<string, string> Packages { get; set; }
        
        public List<string> Preseeds { get; set; }
    }
}