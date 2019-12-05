using AptTool.Apt;

namespace AptTool.Workspace
{
    public class ImageAptRepo : AptRepo
    {
        public ImageAptRepo(bool trusted, string uri, string distribution, bool source, params string[] components) : base(trusted, uri, distribution, source, components)
        {
        }
        
        public bool IncludeSourcePackages { get; set; }
    }
}