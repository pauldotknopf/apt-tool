using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AptTool.Apt
{
    public abstract class PackageDependency
    {
        public abstract bool SatisfiedBy(string packageName);
    }

    public class PackageDependencyAlternates : PackageDependency
    {
        public PackageDependencyAlternates(IList<string> packages)
        {
            Packages = new ReadOnlyCollection<string>(packages.ToList());
        }

        public ReadOnlyCollection<string> Packages { get; set; }

        public override bool SatisfiedBy(string packageName)
        {
            return Packages.Contains(packageName);
        }
    }

    public class PackageDependencySpecific : PackageDependency
    {
        public PackageDependencySpecific(string package)
        {
            Package = package;
        }
        
        public string Package { get; }

        public override bool SatisfiedBy(string packageName)
        {
            return Package == packageName;
        }
    }
}