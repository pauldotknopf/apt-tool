using ServiceStack.DataAnnotations;

namespace AptTool.Security
{
    [Alias("package_notes")]
    public class PackageNotes
    {
        [Alias("id")]
        public int Id { get; set; }
        
        [Alias("bug_name")]
        public string BugName { get; set; }
        
        [Alias("package")]
        public string Package { get; set; }
        
        [Alias("fixed_version")]
        public string FixedVersion { get; set; }
        
        [Alias("fixed_version_id")]
        public int? FixedVersionId { get; set; }
        
        [Alias("release")]
        public string Release { get; set; }
        
        [Alias("package_kind")]
        public string PackageKind { get; set; }
        
        [Alias("urgency")]
        public string Urgency { get; set; }
    }
}