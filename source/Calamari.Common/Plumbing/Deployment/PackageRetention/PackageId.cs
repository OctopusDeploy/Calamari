using System;
using Calamari.Deployment.PackageRetention;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class PackageId : CaseInsensitiveTinyType
    {
        public PackageId(string value) : base(value)
        {
        }
    }
}