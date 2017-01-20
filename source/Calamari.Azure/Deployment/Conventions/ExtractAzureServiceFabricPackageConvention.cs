using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;

namespace Calamari.Azure.Deployment.Conventions
{
    public class ExtractAzureServiceFabricPackageConvention : IInstallConvention
    {
        readonly IPackageExtractor extractor;
        readonly ICalamariFileSystem fileSystem;
        readonly string packagePath;

        public ExtractAzureServiceFabricPackageConvention(IPackageExtractor extractor,
            ICalamariFileSystem fileSystem,
            string packagePath)
        {
            this.extractor = extractor;
            this.fileSystem = fileSystem;
            this.packagePath = packagePath;
        }

        public void Install(RunningDeployment deployment)
        {
            //if (string.IsNullOrWhiteSpace(packagePath))
            //{
            //    throw new Exception("No Azure Fabric Application package path defined.");
            //    Log.Verbose("No Azure Fabric Application package path defined.");
            //    return;
            //}

            //var metadata = extractor.GetMetadata(packagePath);

            //var targetPath = GetTargetPath(deployment, metadata);

            //Log.Verbose("Extracting package to: " + targetPath);

            //var filesExtracted = extractor.Extract(packagePath, targetPath, deployment.Variables.GetFlag(SpecialVariables.Package.SuppressNestedScriptWarning, false));

            //Log.Verbose("Extracted " + filesExtracted + " files");

            //deployment.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, targetPath);
            //Log.SetOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, targetPath, deployment.Variables);
            //Log.SetOutputVariable(SpecialVariables.Package.Output.DeprecatedInstallationDirectoryPath, targetPath, deployment.Variables);






            //Log.VerboseFormat("Extracting Azure Fabric Application package: '{0}'", packagePath);

            ////TODO: markse - Consider the implications of package extraction to a hardcoded directory (ie. multiple SF apps in the one deployment working directory won't be able to run in parallel).
            ////TODO: markse - Use the package name for this extraction path instead.

            //var workingDirectory = deployment.CurrentDirectory;
            //var destinationPath = Path.Combine(workingDirectory, "OctopusAzureServiceFabricPackage");
            //using (var package = Package.Open(packagePath, FileMode.Open))
            //{
            //    // TODO: markse - confirm how best to extract a package that could be in any package format.
            //    ExtractContents(package, destinationPath);
            //}

            //if (deployment.Variables.GetFlag(SpecialVariables.Action.Azure.FabricLogExtractedApplicationPackage))
            //    LogExtractedPackage(destinationPath);

            //Log.SetOutputVariable(SpecialVariables.Action.Azure.FabricApplicationPackagePath,
            //    destinationPath,
            //    deployment.Variables);
        }
        
    }
}
