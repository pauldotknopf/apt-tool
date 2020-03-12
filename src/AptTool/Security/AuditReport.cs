using System.Collections.Generic;
using AptTool.Apt;

namespace AptTool.Security
{
    public class AuditReport
    {
        public AuditReport()
        {
            Sources = new List<AuditSourcePackage>();
        }
        
        public List<AuditSourcePackage> Sources { get; set; }
        
        public class AuditSourcePackage
        {
            public AuditSourcePackage()
            {
                Binaries = new Dictionary<string, AptVersion>();
                Vulnerabilities = new List<Vulnerability>();
            }
            
            public string Name { get; set; }
            
            public string Version { get; set; }
            
            public Dictionary<string, AptVersion> Binaries { get; }
            
            public List<Vulnerability> Vulnerabilities { get; }
        }
        
        public class Vulnerability
        {
            public string Name { get; set; }
            
            public string Description { get; set; }
            
            public string Severity { get; set; }
            
            public string Link { get; set; }
            
            public List<string> Notes { get; set; }
            
            public string FixedVersion { get; set; }
            
            public List<string> References { get; set; }
            
            public NoDsa NoDsa { get; set; }
        }

        public class NoDsa
        {
            public string Reason { get; set; }
            
            public string Comment { get; set; }
        }
    }
}