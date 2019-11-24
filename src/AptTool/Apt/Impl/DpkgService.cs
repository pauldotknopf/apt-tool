using System;
using System.Collections.Generic;
using System.Linq;
using AptTool.Process;
using BindingAttributes;

namespace AptTool.Apt.Impl
{
    [Binding(typeof(IDpkgService))]
    public class DpkgService : IDpkgService
    {
        private readonly IProcessRunner _processRunner;

        public DpkgService(IProcessRunner processRunner)
        {
            _processRunner = processRunner;
        }
        
        public List<string> Extract(string debFile, string directory, bool useSudo)
        {
            var output = _processRunner.ReadShell($"dpkg -X {debFile} {directory}", new RunnerOptions
            {
                UseSudo = useSudo,
                Env = new Dictionary<string, string>
                {
                    { "LC_ALL", "C" }
                }
            });
            return output.Split(Environment.NewLine).Where(x => !string.IsNullOrEmpty(x)).ToList();
        }
    }
}