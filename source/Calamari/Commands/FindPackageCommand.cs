using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Octopus.Versioning.Metadata;

namespace Calamari.Commands
{
    [Command("find-package", Description = "Finds the package that matches the specified ID and version. If no exact match is found, it returns a list of the nearest packages that matches the ID")]
    public class FindPackageCommand : Command
    {
        string packageId;
        string packageVersion;
        string packageHash;
        bool exactMatchOnly;

        public FindPackageCommand()
        {
            Options.Add("packageId=", "Package ID to find", v => packageId = v);
            Options.Add("packageVersion=", "Package version to find", v => packageVersion = v);
            Options.Add("packageHash=", "Package hash to compare against", v => packageHash = v);
            Options.Add("exactMatch=", "Only return exact matches", v => exactMatchOnly = bool.Parse(v));
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(packageId, "No package ID was specified. Please pass --packageId YourPackage");
            Guard.NotNullOrWhiteSpace(packageVersion, "No package version was specified. Please pass --packageVersion 1.0.0.0");
            Guard.NotNullOrWhiteSpace(packageHash, "No package hash was specified. Please pass --packageHash YourPackageHash");
            
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var packageMetadata = new MetadataFactory().GetMetadataFromPackageID(packageId, packageVersion, null, 0, packageHash);
            
            var extractor = new GenericPackageExtractorFactory().createJavaGenericPackageExtractor(fileSystem);
            var packageStore = new PackageStore(extractor);                        
            var package = packageStore.GetPackage(packageMetadata);
                      
            if (package == null)
            {
                Log.Verbose($"Package {packageMetadata.PackageId} version {packageMetadata.Version} hash {packageMetadata.Hash} has not been uploaded.");

                if (exactMatchOnly)
                    return 0;

                FindEarlierPackages(packageStore, packageMetadata);

                return 0;
            }

            Log.VerboseFormat("Package {0} {1} hash {2} has already been uploaded", package.Metadata.PackageId, package.Metadata.Version, package.Metadata.Hash);
            Log.ServiceMessages.PackageFound(
                package.Metadata.PackageId, 
                package.Metadata.Version,
                package.Metadata.Hash, 
                package.Metadata.FileExtension,
                package.FullPath, 
                true);
            return 0;                                   
        }

        void FindEarlierPackages(PackageStore packageStore, PhysicalPackageMetadata packageMetadata)
        {
            Log.VerboseFormat("Finding earlier packages that have been uploaded to this Tentacle.");
            var nearestPackages = packageStore.GetNearestPackages(packageMetadata).ToList();
            if (!nearestPackages.Any())
            {
                Log.VerboseFormat("No earlier packages for {0} has been uploaded", packageId);
            }

            Log.VerboseFormat("Found {0} earlier {1} of {2} on this Tentacle", 
                nearestPackages.Count, nearestPackages.Count == 1 ? "version" : "versions", packageId);
            foreach(var nearestPackage in nearestPackages)
            {
                Log.VerboseFormat("  - {0}: {1}", nearestPackage.Metadata.Version, nearestPackage.FullPath);
                Log.ServiceMessages.PackageFound(
                    nearestPackage.Metadata.PackageId, 
                    nearestPackage.Metadata.Version, 
                    nearestPackage.Metadata.Hash,
                    nearestPackage.Metadata.FileExtension,
                    nearestPackage.FullPath);
            }
        }
    }
}