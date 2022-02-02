using System;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
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