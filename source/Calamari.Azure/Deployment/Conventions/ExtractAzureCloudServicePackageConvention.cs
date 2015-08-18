using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using Calamari.Azure.Integration.CloudServicePackage;
using Calamari.Azure.Integration.CloudServicePackage.ManifestSchema;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;

namespace Calamari.Azure.Deployment.Conventions
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

            var packagePath = deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServicePackagePath);
            Log.VerboseFormat("Extracting Cloud Service package: '{0}'", packagePath);
            using (var package = Package.Open(packagePath, FileMode.Open))
            {
                var manifest = AzureCloudServiceConventions.ReadPackageManifest(package);
                var workingDirectory = deployment.CurrentDirectory;

                ExtractContents(package, manifest, AzureCloudServiceConventions.PackageFolders.ServiceDefinition, workingDirectory);
                ExtractContents(package, manifest, AzureCloudServiceConventions.PackageFolders.NamedStreams, workingDirectory);
                ExtractLayouts(package, manifest, workingDirectory);
            }

            if (deployment.Variables.GetFlag(SpecialVariables.Action.Azure.LogExtractedCspkg))
                LogExtractedPackage(deployment.CurrentDirectory);
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
            var localContentDirectory = Path.Combine(workingDirectory, AzureCloudServiceConventions.PackageFolders.LocalContent);
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

        void LogExtractedPackage(string workingDirectory)
        {
            Log.Verbose("CSPKG extracted. Working directory contents:");
            LogDirectoryContents(workingDirectory, "", 0);
        }

        void LogDirectoryContents(string workingDirectory, string currentDirectoryRelativePath, int depth = 0)
        {
            var directory = new DirectoryInfo(Path.Combine(workingDirectory, currentDirectoryRelativePath));

            var files = fileSystem.EnumerateFiles(directory.FullName).ToList();
            for (int i = 0; i < files.Count; i++)
            {
                // Only log the first 7 files in each directory
                if (i == 7)
                {
                    Log.VerboseFormat("{0}And {1} more files...", Tabs(depth), files.Count - i);
                    break;
                }

                var file = files[i];
                Log.Verbose(Tabs(depth) + Path.GetFileName(file));
            }

            foreach (var subDirectory in fileSystem.EnumerateDirectories(directory.FullName).Select(x => new DirectoryInfo(x)))
            {
                Log.Verbose(Tabs(depth) + "\\" + subDirectory.Name);
                LogDirectoryContents(workingDirectory, Path.Combine(currentDirectoryRelativePath, subDirectory.Name), depth + 1);
            }
        }

        static string Tabs(int n)
        {
            var tabs = new StringBuilder();
            for (int i = 0; i < n; i++)
                tabs.Append("   ");

            return tabs.ToString();
        }
    }
}