using System;
using AptTool.Workspace;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace AptTool.Commands
{
    public class DebianSecurityAudit
    {
        [Verb("debian-security-audit")]
        public class Command : Program.CommonOptions
        {
            [Option('s', "suite", Required = false, Default = "buster")]
            public string Suite { get; set; }
            
            [Option('d', "database", Required = true)]
            public string Database { get; set; }
        }

        public static int Run(Command command)
        {
            var sp = Program.BuildServiceProvider(command);
            var workspace = sp.GetRequiredService<IWorkspace>();

            workspace.SaveAuditReport(command.Suite, command.Database);
            
            return 0;
        }
    }
}