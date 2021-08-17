using System.Security.Cryptography.X509Certificates;
using Octopus.Versioning;

namespace Calamari.Deployment.PackageRetention
{
    public class PackageID : CaseInsensitiveTinyType
    {
        public PackageID(string value) : base(value)
        {
        }
    }
}