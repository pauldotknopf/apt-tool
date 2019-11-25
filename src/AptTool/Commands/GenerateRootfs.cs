using System;
using AptTool.Workspace;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace AptTool.Commands
{
    public class GenerateRootFs
    {
        [Verb("generate-rootfs", HelpText = "Generate the rootfs.")]
        public class Command : Program.CommonOptions
        {
            [Option('d', "directory", HelpText = "The directory to generate the rootfs to.")]
            public string Directory { get; set; }
            
            [Option('w', "overwrite", HelpText = "Should we ignore (delete) any data that may already be present?")]
            public bool OverWrite { get; set; }
            
            [Option('r', "run-stage2", HelpText = "Should we automatically chroot into the rootfs to run the stage2 scripts?")]
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