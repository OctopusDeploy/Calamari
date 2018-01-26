using System;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Octopus.Versioning;
using Octopus.Versioning.Factories;

namespace Calamari.Commands
{
    [Command("find-package", Description = "Finds the package that matches the specified ID and version. If no exact match is found, it returns a list of the nearest packages that matches the ID")]
    public class FindPackageCommand : Command
    {
        string packageId;
        string rawPackageVersion;
        string packageHash;
        bool exactMatchOnly;
        VersionFormat versionFormat = VersionFormat.Semver;

        public FindPackageCommand()
        {
            Options.Add("packageId=", "Package ID to find", v => packageId = v);
            Options.Add("packageVersion=", "Package version to find", v => rawPackageVersion = v);
            Options.Add("packageHash=", "Package hash to compare against", v => packageHash = v);
            Options.Add("packageVersionFormat=", $"[Optional] Format of version. Options {string.Join(", ", Enum.GetNames(typeof(VersionFormat)))}. Defaults to `{VersionFormat.Semver}`.",
                v =>
                {
                    if (!Enum.TryParse(v, out VersionFormat format))
                    {
                        throw new CommandException($"The provided version format `{format}` is not recognised.");
                    }
                    versionFormat = format;
                });
            Options.Add("exactMatch=", "Only return exact matches", v => exactMatchOnly = bool.Parse(v));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(packageId, "No package ID was specified. Please pass --packageId YourPackage");
            Guard.NotNullOrWhiteSpace(rawPackageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");
            Guard.NotNullOrWhiteSpace(packageHash, "No package hash was specified. Please pass --packageHash YourPackageHash");
            
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var extractor = new GenericPackageExtractorFactory().createJavaGenericPackageExtractor(fileSystem);
            var packageStore = new PackageStore(extractor);

            if (!VersionFactory.TryCreateVersion(rawPackageVersion, out IVersion version, versionFormat))
            {
                throw new CommandException($"Package version '{rawPackageVersion}' is not a valid {versionFormat} version string. Please pass --packageVersionFormat with a different version type.");
            };

            var package = packageStore.GetPackage(packageId, version, packageHash);

            if (package == null)
            {
                Log.Verbose($"Package {packageId} version {version} hash {packageHash} has not been uploaded.");

                if (exactMatchOnly)
                    return 0;

                FindEarlierPackages(packageStore, version);

                return 0;
            }

            Log.VerboseFormat("Package {0} {1} hash {2} has already been uploaded", package.PackageId, package.Version, package.Hash);
            Log.ServiceMessages.PackageFound(
                package.PackageId, 
                package.Version,
                package.Hash, 
                package.Extension,
                package.FullFilePath, 
                true);
            return 0;
        }

        void FindEarlierPackages(PackageStore packageStore, IVersion version)
        {
            Log.VerboseFormat("Finding earlier packages that have been uploaded to this Tentacle.");
            var nearestPackages = packageStore.GetNearestPackages(packageId, version).ToList();
            if (!nearestPackages.Any())
            {
                Log.VerboseFormat("No earlier packages for {0} has been uploaded", packageId);
            }

            Log.VerboseFormat("Found {0} earlier {1} of {2} on this Tentacle", 
                nearestPackages.Count, nearestPackages.Count == 1 ? "version" : "versions", packageId);
            foreach(var nearestPackage in nearestPackages)
            {
                Log.VerboseFormat("  - {0}: {1}", nearestPackage.Version, nearestPackage.FullFilePath);
                Log.ServiceMessages.PackageFound(
                    nearestPackage.PackageId,
                    nearestPackage.Version,
                    nearestPackage.Hash,
                    nearestPackage.Extension,
                    nearestPackage.FullFilePath);
            }
        }
    }
}