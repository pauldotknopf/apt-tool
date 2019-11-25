using System.Collections.Generic;

namespace AptTool.Process
{
    public class RunnerOptions
    {
        public bool UseSudo { get; set; }
        
        public Dictionary<string, string> Env { get; set; }
        
        public string WorkingDirectory { get; set; }
    }
}