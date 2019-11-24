using System;

namespace AptTool.Apt
{
    public class AptVersion : IEquatable<AptVersion>
    {
        public AptVersion(string version, string architecture)
        {
            Version = version;
            Architecture = architecture;
        }
        
        public string Version { get; }
        
        public string Architecture { get;  }

        public bool Equals(AptVersion other)
        {
            if (other == null)
            {
                return false;
            }

            return other.GetHashCode().Equals(GetHashCode());
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as AptVersion);
        }

        public override int GetHashCode()
        {
            var hash = 0;
            
            if (!string.IsNullOrEmpty(Version))
            {
                hash += Version.GetHashCode();
            }

            if (!string.IsNullOrEmpty(Architecture))
            {
                hash += Architecture.GetHashCode();
            }

            return hash;
        }

        public static AptVersion Unspecified => new AptVersion(null, null);

        public string ToCommandParameter(string packageName = null)
        {
            var result = "";
            
            if (!string.IsNullOrEmpty(packageName))
            {
                result = $" {packageName}";
            }

            if (!string.IsNullOrEmpty(Architecture))
            {
                result += $":{Architecture}";
            }

            if (!string.IsNullOrEmpty(Version))
            {
                result += $"={Version}";
            }

            return result;
        }
    }
}