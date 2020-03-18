using AptTool.Workspace;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace AptTool.Commands
{
    public class DownloadChangelogs
    {
        [Verb("sync-changelogs", HelpText = "Sync all the changelogs locally.")]
        public class Command : Program.CommonOptions
        {
            public string Directory { get; set; }
        }

        public static int Run(Command command)
        {
            var sp = Program.BuildServiceProvider(command);
            var workspace = sp.GetRequiredService<IWorkspace>();
            
            workspace.Init();
            workspace.SyncChangelogs();
            
            return 0;
        }
    }
}