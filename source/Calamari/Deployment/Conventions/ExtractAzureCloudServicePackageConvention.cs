using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using Calamari.Integration.Azure.CloudServicePackage;
using Calamari.Integration.Azure.CloudServicePackage.ManifestSchema;
using Calamari.Integration.FileSystem;

namespace Calamari.Deployment.Conventions
{
    public class ExtractAzureCloudServicePackageConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;

        public ExtractAzureCloudServicePackageConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Action.Azure.CloudServicePackageExtractionDisabled, false))
                return;

            Log.Verbose("Extracting cspkg");
            var packagePath = deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServicePackagePath);
            using (var package = Package.Open(packagePath, FileMode.Open))
            {
                var manifest = AzureCloudServiceConventions.ReadPackageManifest(package);
                var workingDirectory = deployment.CurrentDirectory;

                ExtractContents(package, manifest, "ServiceDefinition", workingDirectory);
                ExtractContents(package, manifest, "NamedStreams", workingDirectory);
                ExtractLayouts(package, manifest, workingDirectory);
            }
        }

        void ExtractContents(Package package, PackageDefinition manifest, string contentNamePrefix, string workingDirectory)
        {
            foreach (var namedStreamsContent in manifest.Contents.Where(x => x.Name.StartsWith(contentNamePrefix)))
            {
                var destinationFileName = Path.Combine(workingDirectory, ConvertToWindowsPath(namedStreamsContent.Description.DataStorePath.ToString()).TrimStart('\\'));
                ExtractPart(package.GetPart(PackUriHelper.ResolvePartUri(new Uri("/", UriKind.Relative), namedStreamsContent.Description.DataStorePath)),
                    destinationFileName);
            }
        }

        void ExtractLayouts(Package package, PackageDefinition manifest, string workingDirectory)
        {
            var localContentDirectory = Path.Combine(workingDirectory, "LocalContent");
            fileSystem.EnsureDirectoryExists(localContentDirectory);

            foreach (var layout in manifest.Layouts)
            {
                if (!layout.Name.StartsWith(AzureCloudServiceConventions.RoleLayoutPrefix))
                    continue;

                var layoutDirectory = Path.Combine(localContentDirectory, layout.Name.Substring(AzureCloudServiceConventions.RoleLayoutPrefix.Length));
                fileSystem.EnsureDirectoryExists(layoutDirectory);

                foreach (var fileDefinition in layout.FileDefinitions)
                {
                    var contentDefinition =
                        manifest.GetContentDefinition(fileDefinition.Description.DataContentReference);

                    var destinationFileName = Path.Combine(layoutDirectory, fileDefinition.FilePath.TrimStart('\\'));
                    ExtractPart(
                        package.GetPart(PackUriHelper.ResolvePartUri(new Uri("/", UriKind.Relative),
                            contentDefinition.Description.DataStorePath)),
                        destinationFileName);
                }
            }
        }

        void ExtractPart(PackagePart part, string destinationPath)
        {
            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(destinationPath));

            using (var packageFileStream = part.GetStream())
            using (var destinationFileStream = fileSystem.OpenFile(destinationPath, FileMode.Create))
            {
                packageFileStream.CopyTo(destinationFileStream);
                destinationFileStream.Flush();
            }
        }

        static string ConvertToWindowsPath(string path)
        {
            return path.Replace("/", "\\");
        }
    }
}