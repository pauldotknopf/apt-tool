using System.Collections.Generic;

namespace AptTool.Process
{
    public class RunnerOptions
    {
        public RunnerOptions()
        {
            PrintCommand = true;
        }
        
        public bool PrintCommand { get; set; }
        
        public bool UseSudo { get; set; }
        
        public Dictionary<string, string> Env { get; set; }
        
        public string WorkingDirectory { get; set; }
    }
}