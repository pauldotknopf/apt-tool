using System.Collections.Generic;

namespace AptTool.Apt
{
    public class DebPackageInfo
    {
        public string Essential { get; set; }
        
        public string Priority { get; set; }
        
        public List<PackageDependency> Dependencies { get; set; }
        
        public List<PackageDependency> PreDependencies { get; set; }
        
        public List<PackageDependency> Provides { get; set; }
    }
}