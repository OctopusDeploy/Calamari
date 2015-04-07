using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Newtonsoft.Json;
using NuGet;

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

            if (String.IsNullOrWhiteSpace(packageId))
                throw new CommandException("No package ID was specified. Please pass --packageId YourPackage");

            if (String.IsNullOrWhiteSpace(packageVersion))
                throw new CommandException("No package version was specified. Please pass --packageVersion 1.0.0.0");

            SemanticVersion version;
            if(!SemanticVersion.TryParse(packageVersion, out version))
                throw new CommandException(String.Format("Package version '{0}' is not a valid Semantic Version", packageVersion));

            if(String.IsNullOrWhiteSpace(packageHash))
                throw new CommandException("No package hash was specified. Please pass --packageHash YourPackageHash");

            var packageStore = new PackageStore();
            var packageMetadata = new PackageMetadata {Id = packageId, Version = packageVersion, Hash = packageHash};
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
                    Log.PackageFound(nearestPackage.Metadata.Id, nearestPackage.Metadata.Version, nearestPackage.Metadata.Hash, nearestPackage.FullPath);
                }

                return 0;
            }

            Log.Info("##octopus[calamari-found-package]");
            Log.VerboseFormat("Package {0} {1} hash {2} has already been uploaded", package.Metadata.Id, package.Metadata.Version, package.Metadata.Hash);
            Log.PackageFound(package.Metadata.Id, package.Metadata.Version, package.Metadata.Hash, package.FullPath);
            return 0;
        }
    }
}
