using System;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Util;

namespace Calamari.Deployment.Conventions
{
    public abstract class ExtractPackageConvention : Calamari.Shared.Commands.IConvention
    {
        readonly IPackageExtractor extractor;
        protected readonly ICalamariFileSystem fileSystem;
        private readonly ILog log;

        protected ExtractPackageConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem, ILog log)
        {
            this.extractor = extractor;
            this.fileSystem = fileSystem;
            this.log = log;
        }

       
        void LogAccessDenied()
        {
            log.Error("Failed to extract the package because access to the package was denied. This may have happened because anti-virus software is scanning the file. Try disabling your anti-virus software in order to rule this out.");
        }
        
        protected abstract string GetTargetPath(IExecutionContext deployment);

        public void Run(IExecutionContext deployment)
        {
            if (string.IsNullOrWhiteSpace(deployment.PackageFilePath))
            {
                log.Verbose("No package path defined. Skipping package extraction.");
                return;
            }

            try
            {
                var targetPath = GetTargetPath(deployment);

                log.Verbose("Extracting package to: " + targetPath);

                var filesExtracted = extractor.Extract(deployment.PackageFilePath, targetPath,
                    deployment.Variables.GetFlag(SpecialVariables.Package.SuppressNestedScriptWarning, false));

                log.Verbose("Extracted " + filesExtracted + " files");

                deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, targetPath);
                log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, targetPath,
                    deployment.Variables);
                log.SetOutputVariable(SpecialVariables.Package.Output.DeprecatedInstallationDirectoryPath, targetPath,
                    deployment.Variables);
                log.SetOutputVariable(SpecialVariables.Package.Output.ExtractedFileCount, filesExtracted.ToString(),
                    deployment.Variables);
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
    }
}