using System.Security.Cryptography.X509Certificates;
using Octopus.Versioning;

namespace Calamari.Deployment.PackageRetention
{
    public class PackageId : CaseInsensitiveTinyType
    {
        public PackageId(string value) : base(value)
        {
        }
    }
}