using System;
using AptTool.Workspace;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace AptTool.Commands
{
    public class GenerateRootFs
    {
        [Verb("generate-rootfs")]
        public class Command
        {
            [Option('d', "directory")]
            public string Directory { get; set; }
            
            [Option('w', "overwrite")]
            public bool OverWrite { get; set; }
        }

        public static int Run(IServiceProvider serviceProvider, Command command)
        {
            var workspace = serviceProvider.GetRequiredService<IWorkspace>();
            
            workspace.Init();
            workspace.GenerateRootFs(command.Directory, command.OverWrite);
            
            return 0;
        }
    }
}