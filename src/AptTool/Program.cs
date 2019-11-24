using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AptTool.Process;
using BindingAttributes;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AptTool
{
    class Program
    {
        public static IServiceProvider ServiceProvider;
 
        static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            var services = new ServiceCollection();
            services.Configure<WorkspaceConfig>(config => { config.RootDirectory = Directory.GetCurrentDirectory(); });
            services.AddLogging(loggingBuilder => { loggingBuilder.AddSerilog(Log.Logger); });
            BindingAttribute.ConfigureBindings(services, new List<Assembly>{typeof(Program).Assembly});
            ServiceProvider = services.BuildServiceProvider();

            Env.IsRoot = int.Parse(ServiceProvider.GetRequiredService<IProcessRunner>().ReadShell("id -u", new RunnerOptions
            {
                PrintCommand = false
            })) == 0;
            
            return Parser.Default.ParseArguments<Commands.Install.Command,
                    Commands.GenerateRootFs.Command>(args)
                .MapResult(
                    (Commands.Install.Command opts) => Commands.Install.Run(ServiceProvider, opts),
                    (Commands.GenerateRootFs.Command opts) => Commands.GenerateRootFs.Run(ServiceProvider, opts),
                    errs => 1);
        }
    }
}
