using System;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using NuGet.Versioning;

namespace Calamari.Commands
{
    [Command("find-package", Description = "Finds the package that matches the specified ID and version. If no exact match is found, it returns a list of the nearest packages that matches the ID")]
    public class FindPackageCommand : Command
    {
        string packageId;
        string packageVersion;
        string packageHash;

        public FindPackageCommand()
        {
            Options.Add("packageId=", "Package ID to find", v => packageId = v);
            Options.Add("packageVersion=", "Package version to find", v => packageVersion = v);
            Options.Add("packageHash=", "Package hash to compare against", v => packageHash = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(packageId, "No package ID was specified. Please pass --packageId YourPackage");
            Guard.NotNullOrWhiteSpace(packageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");
            Guard.NotNullOrWhiteSpace(packageHash, "No package hash was specified. Please pass --packageHash YourPackageHash");
            
            NuGetVersion version;
            if(!NuGetVersion.TryParse(packageVersion, out version))
                throw new CommandException(String.Format("Package version '{0}' is not a valid Semantic Version", packageVersion));

            var packageStore = new PackageStore(new GenericPackageExtractor());
            var packageMetadata = new ExtendedPackageMetadata() {Id = packageId, Version = packageVersion, Hash = packageHash};
            var package = packageStore.GetPackage(packageMetadata);
            if (package == null)
            {
                Log.VerboseFormat("Package {0} version {1} hash {2} has not been uploaded.", 
                    packageMetadata.Id, packageMetadata.Version, packageMetadata.Hash);

                Log.VerboseFormat("Finding earlier packages that have been uploaded to this Tentacle.");
                var nearestPackages = packageStore.GetNearestPackages(packageId, version).ToList();
                if (!nearestPackages.Any())
                {
                    Log.VerboseFormat("No earlier packages for {0} has been uploaded", packageId);
                    return 0;
                }

                Log.VerboseFormat("Found {0} earlier {1} of {2} on this Tentacle", 
                    nearestPackages.Count, nearestPackages.Count == 1 ? "version" : "versions", packageId);
                foreach(var nearestPackage in nearestPackages)
                {
                    Log.VerboseFormat("  - {0}: {1}", nearestPackage.Metadata.Version, nearestPackage.FullPath);
                    Log.ServiceMessages.PackageFound(nearestPackage.Metadata.Id, nearestPackage.Metadata.Version, nearestPackage.Metadata.Hash, nearestPackage.Metadata.FileExtension, nearestPackage.FullPath);
                }

                return 0;
            }

            Log.VerboseFormat("Package {0} {1} hash {2} has already been uploaded", package.Metadata.Id, package.Metadata.Version, package.Metadata.Hash);
            Log.ServiceMessages.PackageFound(package.Metadata.Id, package.Metadata.Version, package.Metadata.Hash, package.Metadata.FileExtension, package.FullPath, true);
            return 0;
        }
    }
}
