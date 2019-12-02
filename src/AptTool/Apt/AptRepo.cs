using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AptTool.Apt
{
    public class AptRepo
    {
        public AptRepo(string uri, string distribution, bool source, params string[] components)
        {
            Uri = uri;
            Distribution = distribution;
            if (components.Length == 0)
            {
                throw new Exception("You must provide at least one component.");
            }
            Source = source;
            Components = components.ToList();
        }
        
        public string Uri { get; set; }
        
        public string Distribution { get; set; }

        public bool Source { get; set; }
        
        public List<string> Components { get; set; }

        public override string ToString()
        {
            return $"{(Source ? "deb-src" : "deb")} {Uri} {Distribution} {string.Join(" ", Components)}";
        }
    }
}