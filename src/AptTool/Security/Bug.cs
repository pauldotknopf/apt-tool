using ServiceStack.DataAnnotations;

namespace AptTool.Security
{
    [Alias("bugs")]
    public class Bug
    {
        [Alias("name")]
        public string Name { get; set; }
        
        [Alias("description")]
        public string Description { get; set; }
    }
}