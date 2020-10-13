using System.Collections.Generic;
using AptTool.Workspace;

namespace AptTool.Apt
{
    public interface IAptDirectoryPrepService
    {
        void Prep(string workspaceRoot, List<AptRepo> repositories, bool excludeRecommends);
        
        string AptDirectory { get; }
        
        string AptConfigFile { get; }
    }
}