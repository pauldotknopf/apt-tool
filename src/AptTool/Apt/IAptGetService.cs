using System.Collections.Generic;

namespace AptTool.Apt
{
    public interface IAptGetService
    {
        void Update();

        void Download(Dictionary<string, AptVersion> packages, string directory = null);

        Dictionary<string, AptVersion> SimulateInstall(Dictionary<string, AptVersion> packages);
    }
}