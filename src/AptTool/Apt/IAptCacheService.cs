using System.Collections.Generic;

namespace AptTool.Apt
{
    public interface IAptCacheService
    {
        List<string> Packages();

        Dictionary<string, Dictionary<AptVersion, DebPackageInfo>> Show(Dictionary<string, AptVersion> packages);
        
        Dictionary<string, PolicyInfo> Policy(List<string> packages);
    }
}