using System.Collections.Generic;
using AptTool.Apt;

namespace AptTool.Workspace
{
    public interface IWorkspace
    {
        void Init();

        Image GetImage();

        ImageLock GetImageLock();

        void Install();

        void GenerateRootFs(string directory, bool overwrite, bool runStage2);
    }
}