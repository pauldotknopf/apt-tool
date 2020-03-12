using ServiceStack.DataAnnotations;

namespace AptTool.Security
{
    [Alias("nvd_data")]
    public class NvdData
    {
        [Alias("cve_name")]
        public string CveName { get; set; }
        
        [Alias("cve_desc")]
        public string CveDescription { get; set; }
        
        [Alias("severity")]
        public string Severity { get; set; }
    }
}