using System;
using System.Collections.Generic;
using System.IO;
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
                throw new CommandException("No package hash var specified. Please pass --packageHash YourPackageHash");

            var packageStore = new PackageStore();
            var packageMetadata = new PackageMetadata {Id = packageId, Version = packageVersion, Hash = packageHash};
            var package = packageStore.GetPackage(packageMetadata);
            if (package == null)
            {
                Log.VerboseFormat("Package {0} {1} hash {2} has not been uploaded.");
                Log.Verbose("Finding earlier packages that have been uploaded.");
                foreach(var nearestPackage in packageStore.GetNearestPackages(packageId, version))
                {
                    Log.Info("##octopus[foundPackage id=\"{0}\" version=\"{1}\" hash=\"{2}\" remotePath=\"{3}\"]",
                        Log.ConvertServiceMessageValue(nearestPackage.Metadata.Id),
                        Log.ConvertServiceMessageValue(nearestPackage.Metadata.Version),
                        Log.ConvertServiceMessageValue(nearestPackage.Metadata.Hash),
                        Log.ConvertServiceMessageValue(nearestPackage.FullPath));
                }

                return 0;
            }

            Log.Info("##octopus[calamari-found-package]");
            Log.Info("Package {0} {1} hash {2} has already been uploaded", package.Metadata.Id, package.Metadata.Version, package.Metadata.Hash);
            Log.Info("##octopus[foundPackage id=\"{0}\" version=\"{1}\" hash=\"{2}\" remotePath=\"{3}\"",
                package.Metadata.Id,
                package.Metadata.Version,
                package.Metadata.Hash,
                package.FullPath);
            return 0;
        }
    }
}
