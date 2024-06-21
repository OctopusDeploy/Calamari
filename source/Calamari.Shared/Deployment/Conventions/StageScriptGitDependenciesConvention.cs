using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.Conventions
{
    public class StageScriptGitDependenciesConvention : IInstallConvention
    {
        private readonly string? packagePathContainingScript;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IPackageExtractor extractor;
        private readonly bool forceExtract;

        public StageScriptGitDependenciesConvention(string? packagePathContainingScript, ICalamariFileSystem fileSystem, IPackageExtractor extractor, bool forceExtract = false)
        {
            this.packagePathContainingScript = packagePathContainingScript;
            this.fileSystem = fileSystem;
            this.extractor = extractor;
            this.forceExtract = forceExtract;
        }

        public void Install(RunningDeployment deployment)
        {
            // If the script is contained in a package, then extract the containing package in the working directory
            if (!string.IsNullOrWhiteSpace(packagePathContainingScript))
            {
                ExtractPackage(packagePathContainingScript, deployment.CurrentDirectory);
                deployment.Variables.Set(KnownVariables.OriginalPackageDirectoryPath, deployment.CurrentDirectory);
            }

            // Stage any referenced packages (i.e. packages that don't contain the script)
            // The may or may not be extracted.
            StagePackageReferences(deployment);
        }

        void StagePackageReferences(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            // No need to check for "default" package since it gets extracted in the current directory in previous step.
            var gitResourceReferenceNames = variables.GetIndexes(GitResourceVariables.GitResourceCollection)
                .Where(i => !string.IsNullOrEmpty(i));

            foreach (var gitResourceReferenceName in gitResourceReferenceNames)
            {
                Log.Verbose($"Considering '{gitResourceReferenceName}' for extraction");
                var sanitizedPackageReferenceName = fileSystem.RemoveInvalidFileNameChars(gitResourceReferenceName);

                var originalPath = variables.Get(GitResourceVariables.OriginalPath(gitResourceReferenceName));

                if (string.IsNullOrWhiteSpace(originalPath))
                {
                    Log.Info($"Package '{gitResourceReferenceName}' was not acquired or does not require staging");
                    continue;
                }

                originalPath = Path.GetFullPath(variables.Get(GitResourceVariables.OriginalPath(gitResourceReferenceName))!);

                // In the case of container images, the original path is not a file-path.  We won't try and extract or move it.
                if (!fileSystem.FileExists(originalPath))
                {
                    Log.Verbose($"Package '{gitResourceReferenceName}' was not found at '{originalPath}', skipping extraction");
                    continue;  
                }

                var shouldExtract = variables.GetFlag(GitResourceVariables.ExtractedPath(gitResourceReferenceName));

                if (forceExtract || shouldExtract)
                {
                    var extractionPath = Path.Combine(deployment.CurrentDirectory, sanitizedPackageReferenceName);
                    ExtractPackage(originalPath, extractionPath);
                    Log.SetOutputVariable(SpecialVariables.GitResources.ExtractedPath(gitResourceReferenceName), extractionPath, variables);
                }
                else
                {
                    var localPackageFileName = sanitizedPackageReferenceName + Path.GetExtension(originalPath);
                    var destinationPackagePath = Path.Combine(deployment.CurrentDirectory, localPackageFileName);
                    Log.Info($"Copying package: '{originalPath}' -> '{destinationPackagePath}'");
                    fileSystem.CopyFile(originalPath, destinationPackagePath);
                    
                    Log.SetOutputVariable(SpecialVariables.GitResources.PackageFilePath(gitResourceReferenceName), destinationPackagePath, variables);
                    Log.SetOutputVariable(SpecialVariables.GitResources.PackageFileName(gitResourceReferenceName), localPackageFileName, variables);
                }
            }
        }

        void ExtractPackage(string packageFile, string extractionDirectory)
        {
           Log.Info($"Extracting package '{packageFile}' to '{extractionDirectory}'");

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);

            extractor.Extract(packageFile, extractionDirectory);
        }
    }
}