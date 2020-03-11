using System.Collections.Generic;

namespace AptTool.Apt
{
    public class DebPackageInfo
    {
        public string Essential { get; set; }
        
        public string Priority { get; set; }
        
        public string SourcePackage { get; set; }
        
        public string SourceVersion { get; set; }
    }
}