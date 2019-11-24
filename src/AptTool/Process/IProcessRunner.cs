namespace AptTool.Process
{
    public interface IProcessRunner
    {
        void RunShell(string command, RunnerOptions runnerOptions = null);
        
        string ReadShell(string command, RunnerOptions runnerOptions = null);
    }
}