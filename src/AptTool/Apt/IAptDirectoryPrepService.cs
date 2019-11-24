using System.Collections.Generic;

namespace AptTool.Apt
{
    public interface IAptDirectoryPrepService
    {
        void Prep(string workspaceRoot, List<AptRepo> repositories);
        
        string AptDirectory { get; }
        
        string AptConfigFile { get; }
    }
}