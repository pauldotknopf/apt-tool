using ServiceStack.DataAnnotations;

namespace AptTool.Security
{
    [Alias("bugs_xref")]
    public class BugsXref
    {
        [Alias("source")]
        public string Source { get; set; }
        
        [Alias("target")]
        public string Target { get; set; }
    }
}