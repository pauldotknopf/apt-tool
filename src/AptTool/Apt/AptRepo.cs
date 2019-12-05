using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AptTool.Apt
{
    public class AptRepo
    {
        public AptRepo(bool trusted, string uri, string distribution, bool source, params string[] components)
        {
            Trusted = trusted;
            Uri = uri;
            Distribution = distribution;
            Source = source;
            Components = components != null ? components.ToList() : new List<string>();
        }
        
        public bool Trusted { get; set; }
        
        public string Uri { get; set; }
        
        public string Distribution { get; set; }

        public bool Source { get; set; }
        
        public List<string> Components { get; set; }

        public override string ToString()
        {
            var result = new StringBuilder();
            result.Append(Source ? "deb-src " : "deb ");
            if (Trusted)
            {
                result.Append("[trusted=yes] ");
            }
            result.Append($"{Uri} {Distribution} {string.Join(" ", Components)}".Trim());
            return result.ToString();
        }
    }
}