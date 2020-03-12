using ServiceStack.DataAnnotations;

namespace AptTool.Security
{
    [Alias("package_notes_nodsa")]
    public class PackageNotesNoDsa
    {
        [Alias("bug_name")]
        public string BugName { get; set; }
        
        [Alias("package")]
        public string Package { get; set; }
        
        [Alias("release")]
        public string Release { get; set; }
        
        [Alias("reason")]
        public string Reason { get; set; }
        
        [Alias("comment")]
        public string Comment { get; set; }
    }
}