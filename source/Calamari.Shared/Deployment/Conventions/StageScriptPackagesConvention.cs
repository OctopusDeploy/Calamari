using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions.DependencyVariablesStrategies;

namespace Calamari.Deployment.Conventions
{
    public class StageScriptPackagesConvention : IInstallConvention
    {
        readonly string? packagePathContainingScript;
        readonly ICalamariFileSystem fileSystem;
        readonly IPackageExtractor extractor;
        readonly IDependencyVariablesStrategy dependencyVariablesStrategy;
        readonly bool forceExtract;

        public StageScriptPackagesConvention(string? packagePathContainingScript, ICalamariFileSystem fileSystem, IPackageExtractor extractor, IDependencyVariablesStrategy dependencyVariablesStrategy, bool forceExtract = false)
        {
            this.packagePathContainingScript = packagePathContainingScript;
            this.fileSystem = fileSystem;
            this.extractor = extractor;
            this.dependencyVariablesStrategy = dependencyVariablesStrategy;
            this.forceExtract = forceExtract;
        }

        public void Install(RunningDeployment deployment)
        {
            // If the script is contained in a dependency, then extract the containing dependency in the working directory
            if (!string.IsNullOrWhiteSpace(packagePathContainingScript))
            {
                ExtractDependency(packagePathContainingScript, deployment.CurrentDirectory);
                deployment.Variables.Set(KnownVariables.OriginalPackageDirectoryPath, deployment.CurrentDirectory);
            }

            // Stage any referenced dependencies (i.e. dependencies that don't contain the primary files)
            // The may or may not be extracted.
            StageDependencyReferences(deployment);
        }

        void StageDependencyReferences(RunningDeployment deployment)
        {
            var variables = dependencyVariablesStrategy.GetDependencyVariables(deployment.Variables);

            // No need to check for "default" dependency since it gets extracted in the current directory in previous step.
            var referenceNames = variables.GetIndexes();

            foreach (var referenceName in referenceNames)
            {
                Log.Verbose($"Considering '{referenceName}' for extraction");

                var originalRelativePath = variables.OriginalPath(referenceName);

                if (string.IsNullOrWhiteSpace(originalRelativePath))
                {
                    Log.Info($"Dependency '{referenceName}' was not acquired or does not require staging");
                    continue;
                }

                var originalFullPath = Path.GetFullPath(variables.OriginalPath(referenceName));

                // In the case of container images, the original path is not a file-path.  We won't try and extract or move it.
                if (!fileSystem.FileExists(originalFullPath))
                {
                    Log.Verbose($"Dependency '{referenceName}' was not found at '{originalFullPath}', skipping extraction");
                    continue;
                }

                var shouldExtract = variables.Extract(referenceName);                              
                var sanitizedReferenceName = fileSystem.RemoveInvalidFileNameChars(referenceName);

                if (forceExtract || shouldExtract)
                {
                    var extractionPath = Path.Combine(deployment.CurrentDirectory, sanitizedReferenceName);
                    ExtractDependency(originalFullPath, extractionPath);
                    Log.SetOutputVariable(variables.OutputVariables.ExtractedPath(referenceName), extractionPath, deployment.Variables);
                }
                else
                {
                    var localFileName = sanitizedReferenceName + Path.GetExtension(originalFullPath);
                    var destinationPath = Path.Combine(deployment.CurrentDirectory, localFileName);
                    Log.Info($"Copying dependency: '{originalFullPath}' -> '{destinationPath}'");
                    fileSystem.CopyFile(originalFullPath, destinationPath);
                    Log.SetOutputVariable(variables.OutputVariables.FilePath(referenceName), destinationPath, deployment.Variables);
                    Log.SetOutputVariable(variables.OutputVariables.FileName(referenceName), localFileName, deployment.Variables);
                }
            }
        }

        void ExtractDependency(string file, string extractionDirectory)
        {
           Log.Info($"Extracting dependency '{file}' to '{extractionDirectory}'");

            if (!File.Exists(file))
                throw new CommandException("Could not find dependency file: " + file);

            extractor.Extract(file, extractionDirectory);
        }
    }
}