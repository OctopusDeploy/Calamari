using Octopus.TinyTypes;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class PackagePath : CaseInsensitiveStringTinyType
    {
        public PackagePath(string value) : base(value)
        {
        }
    }
}