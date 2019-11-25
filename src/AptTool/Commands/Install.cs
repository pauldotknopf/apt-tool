using System;
using AptTool.Workspace;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace AptTool.Commands
{
    public class Install
    {
        [Verb("install", HelpText = "Regenerate the image-lock.json, based on the image.json and repositories.json files.")]
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