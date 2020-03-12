using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AptTool.Process;
using AptTool.Process.Impl;
using BindingAttributes;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mono.Unix.Native;
using Serilog;
using Serilog.Events;

namespace AptTool
{
    public class Program
    {
        public class CommonOptions
        {
            [Option('v', "verbose", Required = false, HelpText = "Display some useful information, for debugging purposes.")]
            public bool Verbose { get; set; }
        }
 
        static int Main(string[] args)
        {
            // Bug starting gzip within process, see this: https://blog.nelhage.com/2010/02/a-very-subtle-bug/
            Stdlib.SetSignalAction(Signum.SIGPIPE, SignalAction.Default);
            
            return Parser.Default.ParseArguments<Commands.Install.Command,
                    Commands.GenerateRootFs.Command,
                    Commands.Man.Command,
                    Commands.DownloadChangelogs.Command,
                    Commands.DebianSecurityAudit.Command>(args)
                .MapResult(
                    (Commands.Install.Command opts) => Commands.Install.Run(opts),
                    (Commands.GenerateRootFs.Command opts) => Commands.GenerateRootFs.Run(opts),
                    (Commands.Man.Command opts) => Commands.Man.Run(opts),
                    (Commands.DownloadChangelogs.Command opts) => Commands.DownloadChangelogs.Run(opts),
                    (Commands.DebianSecurityAudit.Command opts) => Commands.DebianSecurityAudit.Run(opts),
                    errs => 1);
        }

        public static IServiceProvider BuildServiceProvider(CommonOptions commonOptions)
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Is(commonOptions.Verbose ? LogEventLevel.Verbose : LogEventLevel.Information).WriteTo.Console().CreateLogger();

            var services = new ServiceCollection();
            services.Configure<WorkspaceConfig>(config => { config.RootDirectory = Directory.GetCurrentDirectory(); });
            services.AddLogging(loggingBuilder => { loggingBuilder.AddSerilog(Log.Logger); });
            BindingAttribute.ConfigureBindings(services, new List<Assembly>{typeof(Program).Assembly});
            
            var sp = services.BuildServiceProvider();

            Env.IsRoot = int.Parse(sp.GetRequiredService<IProcessRunner>().ReadShell("id -u")) == 0;

            return sp;
        }
    }
}
