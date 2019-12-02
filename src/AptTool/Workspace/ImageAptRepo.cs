using AptTool.Apt;

namespace AptTool.Workspace
{
    public class ImageAptRepo : AptRepo
    {
        public ImageAptRepo(string uri, string distribution, bool source, params string[] components) : base(uri, distribution, source, components)
        {
        }
        
        public bool IncludeSourcePackages { get; set; }
    }
}