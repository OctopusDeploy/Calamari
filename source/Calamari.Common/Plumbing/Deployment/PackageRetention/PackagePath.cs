using Calamari.Deployment.PackageRetention;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class PackagePath : CaseInsensitiveTinyType
    {
        public PackagePath(string value) : base(value)
        {
        }
    }
}