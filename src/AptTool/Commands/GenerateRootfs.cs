using System;
using AptTool.Workspace;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace AptTool.Commands
{
    public class GenerateRootFs
    {
        [Verb("generate-rootfs")]
        public class Command : Program.CommonOptions
        {
            [Option('d', "directory")]
            public string Directory { get; set; }
            
            [Option('w', "overwrite")]
            public bool OverWrite { get; set; }
            
            [Option('r', "run-stage2")]
            public bool RunStage2 { get; set; }
        }

        public static int Run(Command command)
        {
            var sp = Program.BuildServiceProvider(command);
            var workspace = sp.GetRequiredService<IWorkspace>();
            
            workspace.Init();
            workspace.GenerateRootFs(command.Directory, command.OverWrite, command.RunStage2);
            
            return 0;
        }
    }
}