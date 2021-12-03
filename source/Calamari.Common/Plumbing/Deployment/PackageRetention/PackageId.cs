using System;
using System.Security.Cryptography.X509Certificates;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Variables;
using Octopus.Versioning;

namespace Calamari.Deployment.PackageRetention
{
    public class PackageId : CaseInsensitiveTinyType
    {
        public PackageId(string value) : base(value)
        {
        }

        public static PackageId CreatePackageId(string? packageId, IVariables variables, string[] commandLineArguments)
        {
            string? parsedPackageId = null;
            commandLineArguments.ParseArgument("packageId", s => parsedPackageId = s );
            packageId ??= parsedPackageId ?? variables.Get(PackageVariables.PackageId) ?? throw new Exception("Package Id not found.");

            return new PackageId(packageId);
        }
    }
}