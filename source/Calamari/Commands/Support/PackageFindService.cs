using System;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.FileSystem;
using Octopus.Versioning;

namespace Calamari.Commands.Support
{
    public class PackageFindService
    {
        readonly ILog log;
        readonly IPackageStore packageStore;

        public PackageFindService(ILog log, IPackageStore packageStore)
        {
            this.log = log;
            this.packageStore = packageStore;
        }

        public PackagePhysicalFileMetadata FindPackage(PackageFindOptions options)
        {
            Guard.NotNullOrWhiteSpace(options.PackageId, "No package ID was specified. Please pass --packageId YourPackage");
            Guard.NotNullOrWhiteSpace(options.PackageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");
            Guard.NotNullOrWhiteSpace(options.PackageHash, "No package hash was specified. Please pass --packageHash YourPackageHash");

            var version = VersionFactory.TryCreateVersion(options.PackageVersion, options.VersionFormat);
            if (version == null)
                throw new CommandException($"Package version '{options.PackageVersion}' is not a valid {options.VersionFormat} version string. Please pass --packageVersionFormat with a different version type.");

            var package = packageStore.GetPackage(options.PackageId, version, options.PackageHash);
            if (package == null)
            {
                log.Verbose($"Package {options.PackageId} version {version} hash {options.PackageHash} has not been uploaded.");

                if (!options.ExactMatchOnly)
                {
                    FindEarlierPackages(options.PackageId, version, options.VersionFormat);
                }

                return null;
            }

            log.VerboseFormat("Package {0} {1} hash {2} has already been uploaded", package.PackageId, package.Version, package.Hash);
            LogPackageFound(
                package.PackageId,
                package.FileVersion,
                package.Hash,
                package.Extension,
                package.FullFilePath,
                true,
                options.VersionFormat
            );
            return package;
        }

        void FindEarlierPackages(string packageId, IVersion version, VersionFormat versionFormat)
        {
            log.VerboseFormat("Finding earlier packages that have been uploaded to this Tentacle.");
            var nearestPackages = packageStore.GetNearestPackages(packageId, version).ToList();
            if (!nearestPackages.Any())
            {
                log.VerboseFormat("No earlier packages for {0} has been uploaded", packageId);
            }

            log.VerboseFormat("Found {0} earlier {1} of {2} on this Tentacle",
                nearestPackages.Count, nearestPackages.Count == 1 ? "version" : "versions", packageId);
            foreach (var nearestPackage in nearestPackages)
            {
                log.VerboseFormat("  - {0}: {1}", nearestPackage.Version, nearestPackage.FullFilePath);
                LogPackageFound(
                    nearestPackage.PackageId,
                    nearestPackage.FileVersion,
                    nearestPackage.Hash,
                    nearestPackage.Extension,
                    nearestPackage.FullFilePath,
                    false,
                    versionFormat
                );
            }
        }

        void LogPackageFound(
            string packageId,
            IVersion packageVersion,
            string packageHash,
            string packageFileExtension,
            string packageFullPath,
            bool exactMatchExists,
            VersionFormat versionFormat
        )
        {
            if (exactMatchExists)
                log.Verbose("##octopus[calamari-found-package]");

            log.VerboseFormat("##octopus[foundPackage id=\"{0}\" version=\"{1}\" versionFormat=\"{2}\" hash=\"{3}\" remotePath=\"{4}\" fileExtension=\"{5}\"]",
                AbstractLog.ConvertServiceMessageValue(packageId),
                AbstractLog.ConvertServiceMessageValue(packageVersion.ToString()),
                AbstractLog.ConvertServiceMessageValue(packageVersion.Format.ToString()),
                AbstractLog.ConvertServiceMessageValue(packageHash),
                AbstractLog.ConvertServiceMessageValue(packageFullPath),
                AbstractLog.ConvertServiceMessageValue(packageFileExtension));
        }
    }
}
