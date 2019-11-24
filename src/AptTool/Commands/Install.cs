using System;
using AptTool.Workspace;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace AptTool.Commands
{
    public class Install
    {
        [Verb("install")]
        public class Command : Program.CommonOptions
        {
            
        }

        public static int Run(Command command)
        {
            var sp = Program.BuildServiceProvider(command);
            var workspace = sp.GetRequiredService<IWorkspace>();
            
            workspace.Init();
            workspace.Install();
            
            return 0;
        }
    }
}