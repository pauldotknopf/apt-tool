using System.Collections.Generic;
using AptTool.Apt;
using AptTool.Security;

namespace AptTool.Workspace
{
    public interface IWorkspace
    {
        void Init();

        Image GetImage();

        ImageLock GetImageLock();

        void Install();

        void GenerateRootFs(string directory, bool overwrite, bool runStage2);

        void SyncChangelogs();

        void SaveAuditReport(string suite, string database);
    }
}