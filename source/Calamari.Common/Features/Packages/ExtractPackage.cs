using System;
using System.IO;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Packages
{
    public class ExtractPackage : IExtractPackage
    {
        public const string StagingDirectoryName = "staging";
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

        public void ExtractToCustomDirectory(PathToPackage? pathToPackage, string directory)
        {
            ExtractToCustomSubDirectory(pathToPackage, directory, null, null);
        }

        public void ExtractToStagingDirectory(PathToPackage? pathToPackage, IPackageExtractor? customPackageExtractor = null)
        {
            ExtractToCustomSubDirectory(pathToPackage,
                Path.Combine(Environment.CurrentDirectory, StagingDirectoryName),
                customPackageExtractor, null);
        }

        public void ExtractToStagingDirectory(PathToPackage? pathToPackage, string extractedToPathOutputVariableName)
        {
            ExtractToCustomSubDirectory(
                pathToPackage,
                Path.Combine(Environment.CurrentDirectory, StagingDirectoryName),
                null,
                extractedToPathOutputVariableName);
        }

        public void ExtractToEnvironmentCurrentDirectory(PathToPackage pathToPackage)
        {
            var targetPath = Environment.CurrentDirectory;
            Extract(pathToPackage, targetPath, PackageVariables.Output.InstallationDirectoryPath, null);
        }

        public void ExtractToApplicationDirectory(PathToPackage pathToPackage, IPackageExtractor? customPackageExtractor = null)
        {
            var metadata = PackageName.FromFile(pathToPackage);
            var targetPath = ApplicationDirectory.GetApplicationDirectory(metadata, variables, fileSystem);
            Extract(pathToPackage, targetPath, PackageVariables.Output.InstallationDirectoryPath, customPackageExtractor);
        }

        void ExtractToCustomSubDirectory(PathToPackage? pathToPackage, string directory, IPackageExtractor? customPackageExtractor, string? extractedToPathOutputVariableName)
        {
            fileSystem.EnsureDirectoryExists(directory);
            Extract(pathToPackage, directory, extractedToPathOutputVariableName ?? PackageVariables.Output.InstallationDirectoryPath, customPackageExtractor);
        }

        void Extract(PathToPackage? pathToPackage, string targetPath, string extractedToPathOutputVariableName,  IPackageExtractor? customPackageExtractor)
        {
            try
            {
                var path = (string?)pathToPackage;
                if (string.IsNullOrWhiteSpace(path))
                {
                    log.Verbose("No package path defined. Skipping package extraction.");
                    return;
                }

                log.Verbose("Extracting package to: " + targetPath);

                var extractorToUse = customPackageExtractor ?? combinedPackageExtractor;
                var filesExtracted = extractorToUse.Extract(path, targetPath);

                log.Verbose("Extracted " + filesExtracted + " files");

                variables.Set(KnownVariables.OriginalPackageDirectoryPath, targetPath);
                log.SetOutputVariable(extractedToPathOutputVariableName, targetPath, variables);
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
        {
            log.Error("Failed to extract the package because access to the package was denied. This may have happened because anti-virus software is scanning the file. Try disabling your anti-virus software in order to rule this out.");
        }
    }
}