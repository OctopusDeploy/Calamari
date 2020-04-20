using System;
using System.IO;
using Calamari.Common.Variables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Util;

namespace Calamari.Deployment.Conventions
{
    public class ExtractPackage : IExtractPackage
    {
        readonly ICombinedPackageExtractor combinedPackageExtractor;
        readonly ICalamariFileSystem fileSystem;
        readonly IVariables variables;
        readonly ILog log;

        public ExtractPackage(ICombinedPackageExtractor combinedPackageExtractor, ICalamariFileSystem fileSystem, IVariables variables, ILog log)
        {
            this.combinedPackageExtractor = combinedPackageExtractor;
            this.fileSystem = fileSystem;
            this.variables = variables;
            this.log = log;
        }

        public void ExtractToStagingDirectory(PathToPackage pathToPackage, IPackageExtractor customPackageExtractor = null)
        {
            var targetPath = Path.Combine(Environment.CurrentDirectory, "staging");
            fileSystem.EnsureDirectoryExists(targetPath);
            Extract(pathToPackage, targetPath, customPackageExtractor);
        }

        public void ExtractToEnvironmentCurrentDirectory(PathToPackage pathToPackage)
        {
            var targetPath = Environment.CurrentDirectory;
            Extract(pathToPackage, targetPath, null);
        }

        public void ExtractToApplicationDirectory(PathToPackage pathToPackage, IPackageExtractor customPackageExtractor = null)
        {
            var metadata = PackageName.FromFile(pathToPackage);
            var targetPath = ApplicationDirectory.GetApplicationDirectory(metadata, variables, fileSystem);
            Extract(pathToPackage, targetPath, customPackageExtractor);
        }

        void Extract(PathToPackage pathToPackage, string targetPath, IPackageExtractor customPackageExtractor)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pathToPackage))	
                {	
                    log.Verbose("No package path defined. Skipping package extraction.");	
                    return;	
                }
                
                log.Verbose("Extracting package to: " + targetPath);

                var extractorToUse = customPackageExtractor ?? combinedPackageExtractor;
                var filesExtracted = extractorToUse.Extract(pathToPackage, targetPath);

                log.Verbose("Extracted " + filesExtracted + " files");

                variables.Set(Common.Variables.KnownVariables.OriginalPackageDirectoryPath, targetPath);
                log.SetOutputVariable(PackageVariables.Output.InstallationDirectoryPath, targetPath, variables);
                log.SetOutputVariable(PackageVariables.Output.DeprecatedInstallationDirectoryPath, targetPath, variables);
                log.SetOutputVariable(PackageVariables.Output.ExtractedFileCount, filesExtracted.ToString(), variables);
            }
            catch (UnauthorizedAccessException)
            {
                LogAccessDenied();
                throw;
            }
            catch (Exception ex) when (ex.Message.ContainsIgnoreCase("Access is denied"))
            {
                LogAccessDenied();
                throw;
            }
        }

        void LogAccessDenied()
            => log.Error("Failed to extract the package because access to the package was denied. This may have happened because anti-virus software is scanning the file. Try disabling your anti-virus software in order to rule this out.");
    }
}