using System.IO;
using Calamari.Integration.Packages;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class StageScriptPackagesConvention : Calamari.Shared.Commands.IConvention
    {
        private readonly ICalamariFileSystem fileSystem;
        private readonly IGenericPackageExtractor extractor;
        private readonly bool forceExtract;

        public StageScriptPackagesConvention(ICalamariFileSystem fileSystem, IGenericPackageExtractor extractor, bool forceExtract = false)
        {
            
            this.fileSystem = fileSystem;
            this.extractor = extractor;
            this.forceExtract = forceExtract;
        }
        
        public void Run(IExecutionContext deployment)
        {
            var packagePathContainingScript = deployment.PackageFilePath;
            // If the script is contained in a package, then extract the containing package in the working directory 
            if (!string.IsNullOrWhiteSpace(packagePathContainingScript))
            {
                ExtractPackage(packagePathContainingScript, deployment.CurrentDirectory);
                deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, deployment.CurrentDirectory);
            }
            
            // Stage any referenced packages (i.e. packages that don't contain the script) 
            // The may or may not be extracted.
            StagePackageReferences(deployment);
        }

        void StagePackageReferences(IExecutionContext deployment)
        {
            var variables = deployment.Variables;
            
            var packageReferenceNames = variables.GetIndexes(SpecialVariables.Packages.PackageCollection);

            foreach (var packageReferenceName in packageReferenceNames)
            {
                var sanitizedPackageReferenceName = fileSystem.RemoveInvalidFileNameChars(packageReferenceName);
                
                var packageOriginalPath = variables.Get(SpecialVariables.Packages.OriginalPath(packageReferenceName));
                
                if (string.IsNullOrWhiteSpace(packageOriginalPath))
                {
                    Log.Verbose($"Package '{packageReferenceName}' was not acquired");
                    continue;
                }
                
                packageOriginalPath = Path.GetFullPath(variables.Get(SpecialVariables.Packages.OriginalPath(packageReferenceName)));

                // In the case of container images, the original path is not a file-path.  We won't try and extract or move it.
                if (!fileSystem.FileExists(packageOriginalPath))
                    continue;

                var shouldExtract = variables.GetFlag(SpecialVariables.Packages.Extract(packageReferenceName));

                if (forceExtract || shouldExtract)
                {
                    var extractionPath = Path.Combine(deployment.CurrentDirectory, sanitizedPackageReferenceName);
                    ExtractPackage(packageOriginalPath, extractionPath);
                    variables.Set(SpecialVariables.Packages.DestinationPath(packageReferenceName), extractionPath);
                }
                else
                {
                    var localPackageFileName = sanitizedPackageReferenceName + Path.GetExtension(packageOriginalPath);
                    var destinationFileName = Path.Combine(deployment.CurrentDirectory, localPackageFileName);
                    Log.Verbose($"Copying package: '{packageOriginalPath}' -> '{destinationFileName}'");
                    fileSystem.CopyFile(packageOriginalPath, destinationFileName);
                    variables.Set(SpecialVariables.Packages.DestinationPath(packageReferenceName), destinationFileName);
                }
            }
        }

        void ExtractPackage(string packageFile, string extractionDirectory)
        {
           Log.Verbose($"Extracting package '{packageFile}' to '{extractionDirectory}'"); 
            
            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);
            
            extractor.GetExtractor(packageFile).Extract(packageFile, extractionDirectory, true);
        }
    }
}