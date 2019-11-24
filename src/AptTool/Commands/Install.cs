using System;
using AptTool.Workspace;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace AptTool.Commands
{
    public class Install
    {
        [Verb("install")]
        public class Command
        {
            
        }

        public static int Run(IServiceProvider serviceProvider, Command command)
        {
            var workspace = serviceProvider.GetRequiredService<IWorkspace>();
            
            workspace.Init();
            workspace.Install();
            
            return 0;
        }
    }
}