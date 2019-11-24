using System.Collections.Generic;

namespace AptTool.Apt
{
    public interface IDpkgService
    {
        List<string> Extract(string debFile, string directory, bool useSudo);
    }
}