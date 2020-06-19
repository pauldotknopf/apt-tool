using System.Collections.Generic;

namespace AptTool.Apt
{
    public interface IAptCacheService
    {
        List<string> Packages();

        Dictionary<string, Dictionary<AptVersion, DebPackageInfo>> Show(Dictionary<string, AptVersion> packages);
        
        List<string> ImportantAndEssentialPackages();
    }
}