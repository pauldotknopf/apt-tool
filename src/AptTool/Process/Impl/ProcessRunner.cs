using System;
using System.IO;
using BindingAttributes;
using Microsoft.Extensions.Logging;

namespace AptTool.Process.Impl
{
    [Binding(typeof(IProcessRunner))]
    public class ProcessRunner : IProcessRunner
    {
        private readonly ILogger<ProcessRunner> _logger;

        public ProcessRunner(ILogger<ProcessRunner> logger)
        {
            _logger = logger;
        }
        
        public void RunShell(string command, RunnerOptions runnerOptions = null)
        {
            if (runnerOptions == null)
            {
                runnerOptions = new RunnerOptions();
            }

            var workingDirectory = runnerOptions.WorkingDirectory;
            if (string.IsNullOrEmpty(workingDirectory))
            {
                workingDirectory = Directory.GetCurrentDirectory();
            }
            
            var escapedArgs = command.Replace("\"", "\\\"");
            
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/bin/env",
                Arguments = $"{(runnerOptions.UseSudo ? "sudo " : "")}bash -c \"{escapedArgs}\"",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                WorkingDirectory = workingDirectory
            };

            if (runnerOptions.Env != null)
            {
                foreach (var envValue in runnerOptions.Env)
                {
                    processStartInfo.Environment.Add(envValue.Key, envValue.Value);
                }
            }

            var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                throw new Exception("Couldn't create process.");
            }
            
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Error executing command: {command}", command);
                throw new Exception($"Exit code: {process.ExitCode}");
            }
        }

        public string ReadShell(string command, RunnerOptions runnerOptions = null)
        {
            if (runnerOptions == null)
            {
                runnerOptions = new RunnerOptions();
            }
            
            var workingDirectory = runnerOptions.WorkingDirectory;
            if (string.IsNullOrEmpty(workingDirectory))
            {
                workingDirectory = Directory.GetCurrentDirectory();
            }
            
            var escapedArgs = command.Replace("\"", "\\\"");
            
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/bin/env",
                Arguments = $"{(runnerOptions.UseSudo ? "sudo " : "")}bash -c \"{escapedArgs}\"",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardOutput = true,
                WorkingDirectory = workingDirectory
            };

            if (runnerOptions.Env != null)
            {
                foreach (var envValue in runnerOptions.Env)
                {
                    processStartInfo.Environment.Add(envValue.Key, envValue.Value);
                }
            }

            
            var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                throw new Exception("Couldn't create process.");
            }

            var output = process.StandardOutput.ReadToEnd();

            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                _logger.LogError("Error executing command: {command}", command);
                _logger.LogError("Command output: {output}", output);
                throw new Exception($"Exit code: {process.ExitCode}");
            }

            return output;
        }
    }
}