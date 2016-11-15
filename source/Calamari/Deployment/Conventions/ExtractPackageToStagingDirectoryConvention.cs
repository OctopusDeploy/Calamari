using System;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Integration.Packages;
using Calamari.Shared;
using Calamari.Shared.Convention;
using Calamari.Util;
using ICalamariFileSystem = Calamari.Integration.FileSystem.ICalamariFileSystem;

namespace Calamari.Deployment.Conventions
{
    public class ExtractPackageToWorkingDirectoryConvention : ExtractPackageConvention
    {
        public ExtractPackageToWorkingDirectoryConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem)
            : base(extractor, fileSystem)
        {
        }

        protected override string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata)
        {
            return CrossPlatform.GetCurrentDirectory();
        }
    }

    public class ExtractPackageToStagingDirectoryConvention : ExtractPackageConvention
    {
        public ExtractPackageToStagingDirectoryConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem) 
            : base(extractor, fileSystem)
        {
        }

        protected override string GetTargetPath(RunningDeployment deployment, PackageMetadata metadata)
        {
            var targetPath = Path.Combine(CrossPlatform.GetCurrentDirectory(), "staging"); 
            fileSystem.EnsureDirectoryExists(targetPath);
            return targetPath;
        }
    }



    [ConventionMetadata(CommonConventions.PackageExtraction, "Extracts the package", true)]
    public class PackageExtractionConvention : Shared.Convention.IInstallConvention
    {
        private readonly IPackageExtractor extractor;
        private readonly ICalamariFileSystem fileSystem;
        private readonly string packagePath;

        public PackageExtractionConvention(IPackageExtractor extractor, ICalamariFileSystem fileSystem, string packagePath)
        {
            this.extractor = extractor;
            this.fileSystem = fileSystem;
            this.packagePath = packagePath;
        }

        string GetTargetPath(IVariableDictionary variables, PackageMetadata metadata)
        {
            return CrossPlatform.GetCurrentDirectory();
        }

        public void Install(IVariableDictionary variables)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                Log.Verbose("No package path defined. Skipping package extraction.");
                return;
            }

            Log.Info("Extracting package: " + packagePath);

            if (!fileSystem.FileExists(packagePath))
                throw new CommandException("Could not find package file: " + packagePath);

            var metadata = extractor.GetMetadata(packagePath);

            var targetPath = GetTargetPath(variables, metadata);

            Log.Verbose("Extracting package to: " + targetPath);

            var filesExtracted = extractor.Extract(packagePath, targetPath, variables.GetFlag(SpecialVariables.Package.SuppressNestedScriptWarning, false));

            Log.Verbose("Extracted " + filesExtracted + " files");

            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, targetPath);
            Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, targetPath, variables);
            Log.SetOutputVariable(SpecialVariables.Package.Output.DeprecatedInstallationDirectoryPath, targetPath, variables);


        }
    }
}