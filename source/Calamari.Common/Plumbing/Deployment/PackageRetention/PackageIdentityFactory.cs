using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention.VersionFormatDiscovery;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention;
using Octopus.Versioning;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class PackageIdentityFactory
    {
        readonly List<ITryToDiscoverVersionFormat> versionFormatDiscoverers;

        public PackageIdentityFactory(IEnumerable<ITryToDiscoverVersionFormat> versionFormatDiscoverers)
        {
            this.versionFormatDiscoverers = versionFormatDiscoverers.OrderBy(d => d.Priority)
                                                                    .ToList();
        }

        /// <summary>
        /// Creates a PackageIdentity using the information provided to determine the version format..
        /// </summary>
        public PackageIdentity CreatePackageIdentity(IManagePackageUse journal, IVariables variables, string[] commandLineArguments, VersionFormat defaultFormat = VersionFormat.Semver , string? packageId = null, string? version = null )
        {
            var versionStr = version ?? variables.Get(PackageVariables.PackageVersion) ?? throw new Exception("Package Version not found.");
            var packagePath = variables.Get(TentacleVariables.CurrentDeployment.PackageFilePath);
            var packageIdObj = PackageId.CreatePackageId(packageId, variables, commandLineArguments);
            var versionFormat = defaultFormat;
            
            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            versionFormatDiscoverers.FirstOrDefault(d => d.TryDiscoverVersionFormat(journal, variables, commandLineArguments, out versionFormat, defaultFormat));

            return new PackageIdentity(packageIdObj, VersionFactory.CreateVersion(versionStr, versionFormat), packagePath);
        }
    }
}