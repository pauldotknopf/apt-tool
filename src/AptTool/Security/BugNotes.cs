using ServiceStack.DataAnnotations;

namespace AptTool.Security
{
    [Alias("bugs_notes")]
    public class BugNotes
    {
        [Alias("bug_name")]
        public string BugName { get; set; }
        
        [Alias("typ")]
        public string Type { get; set; }
        
        [Alias("release")]
        public string Release { get; set; }
        
        [Alias("comment")]
        public string Comment { get; set; }
    }
}