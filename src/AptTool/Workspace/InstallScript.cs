namespace AptTool.Workspace
{
    public class InstallScript
    {
        public InstallScript()
        {
            Name = "script.sh";
        }
        
        public string Directory { get; set; }
        
        public string Name { get; set; }
    }
}