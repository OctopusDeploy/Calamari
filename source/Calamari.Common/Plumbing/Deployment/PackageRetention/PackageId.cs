using Octopus.TinyTypes;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class PackageId : CaseInsensitiveStringTinyType
    {
        public PackageId(string value) : base(value)
        {
        }
    }
}