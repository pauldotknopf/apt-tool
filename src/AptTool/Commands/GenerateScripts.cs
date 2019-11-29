
using System;
using AptTool.Workspace;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace AptTool.Commands
{
    public class GenerateScripts
    {
        [Verb("generate-scripts", HelpText = "Installs the scripts into the rootfs, to be run.")]
        public class Command : Program.CommonOptions
        {
            [Option('d', "directory", HelpText = "The directory to generate the rootfs to.")]
            public string Directory { get; set; }
            
            [Option('r', "run-scripts", HelpText = "Should we automatically chroot into the rootfs to run the scripts?")]
            public bool RunScripts { get; set; }
        }

        public static int Run(Command command)
        {
            var sp = Program.BuildServiceProvider(command);
            var workspace = sp.GetRequiredService<IWorkspace>();
            
            workspace.Init();
            workspace.GenerateScripts(command.Directory, command.RunScripts);
            
            return 0;
        }
    }
}