using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Calamari.AzureCloudService.CloudServicePackage;
using Calamari.AzureCloudService.CloudServicePackage.ManifestSchema;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureCloudService
{
    public class RePackageCloudServiceBehaviour : IDeployBehaviour
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;

        public RePackageCloudServiceBehaviour(ILog log, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.fileSystem = fileSystem;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return !context.Variables.GetFlag(SpecialVariables.Action.Azure.CloudServicePackageExtractionDisabled);
        }

        public Task Execute(RunningDeployment context)
        {
            log.Verbose("Re-packaging cspkg.");
            var workingDirectory = context.CurrentDirectory;
            var originalPackagePath = context.Variables.Get(SpecialVariables.Action.Azure.CloudServicePackagePath);
            var newPackagePath = Path.Combine(Path.GetDirectoryName(originalPackagePath), Path.GetFileNameWithoutExtension(originalPackagePath) + "_repacked.cspkg");

            using (var originalPackage = Package.Open(originalPackagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var newPackage = Package.Open(newPackagePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                try
                {
                    var originalManifest = AzureCloudServiceConventions.ReadPackageManifest(originalPackage);

                    var newManifest = new PackageDefinition
                    {
                        MetaData = {AzureVersion = originalManifest.MetaData.AzureVersion}
                    };

                    AddParts(newPackage, newManifest,
                        Path.Combine(workingDirectory, AzureCloudServiceConventions.PackageFolders.ServiceDefinition),
                        AzureCloudServiceConventions.PackageFolders.ServiceDefinition);
                    AddParts(newPackage, newManifest,
                        Path.Combine(workingDirectory, AzureCloudServiceConventions.PackageFolders.NamedStreams),
                        AzureCloudServiceConventions.PackageFolders.NamedStreams);
                    AddLocalContent(newPackage, newManifest, workingDirectory);

                    AddPackageManifest(newPackage, newManifest);

                    newPackage.Flush();
                }
                catch (Exception ex)
                {
                    log.Error(ex.PrettyPrint());
                    throw new Exception("An exception occured re-packaging the cspkg");
                }
            }

            fileSystem.OverwriteAndDelete(originalPackagePath, newPackagePath);

            return this.CompletedTask();
        }

        void AddPackageManifest(Package package, PackageDefinition manifest)
        {
            var manifestPartUri = PackUriHelper.CreatePartUri(new Uri("/package.xml", UriKind.Relative));
            var manifestPart = package.CreatePart(manifestPartUri, System.Net.Mime.MediaTypeNames.Application.Octet, CompressionOption.Maximum);
            using (var manifestPartStream = manifestPart.GetStream())
            {
                manifest.ToXml().Save(manifestPartStream);
            }

            package.CreateRelationship(manifestPartUri, TargetMode.External, AzureCloudServiceConventions.CtpFormatPackageDefinitionRelationshipType);
        }

        void AddParts(Package package, PackageDefinition manifest, string directory, string baseDataStorePath)
        {
            foreach (var file in fileSystem.EnumerateFiles(directory))
            {
                var partUri = new Uri(baseDataStorePath + "/" + Path.GetFileName(file), UriKind.Relative);
                AddContent(package, manifest, partUri, file);
            }

            foreach (var subDirectory in fileSystem.EnumerateDirectories(directory).Select(x => new DirectoryInfo(x)))
            {
                AddParts(package, manifest, subDirectory.FullName, baseDataStorePath + "/" + subDirectory.Name);
            }
        }

        void AddLocalContent(Package package, PackageDefinition manifest, string workingDirectory)
        {
            foreach (var roleDirectory in fileSystem.EnumerateDirectories(Path.Combine(workingDirectory, "LocalContent")))
            {
                var layout = new LayoutDefinition {Name = "Roles/" + new DirectoryInfo(roleDirectory).Name};
                manifest.Layouts.Add(layout);
                AddLocalContentParts(package, manifest, layout, roleDirectory, "");
            }
        }

        void AddLocalContentParts(Package package, PackageDefinition manifest, LayoutDefinition layout, string baseDirectory, string relativeDirectory)
        {
            var currentDirectory = Path.Combine(baseDirectory, relativeDirectory);

            foreach (var file in fileSystem.EnumerateFiles(currentDirectory))
            {
                var uniqueFileName = Guid.NewGuid().ToString("N");
                var partUri = new Uri("LocalContent/" + uniqueFileName, UriKind.Relative);
                AddContent(package, manifest, partUri, file);

                //add file definition
                var fileDate = DateTime.UtcNow; //todo: use original timestamps if un-modified
                layout.FileDefinitions.Add(
                    new FileDefinition
                    {
                        FilePath = "\\" + Path.Combine(relativeDirectory, Path.GetFileName(file)),
                        Description =
                            new FileDescription
                            {
                                DataContentReference = partUri.ToString(),
                                ReadOnly = false,
                                Created = fileDate,
                                Modified = fileDate
                            }
                    });
            }

            foreach (var subDirectory in Directory.GetDirectories(currentDirectory).Select(x => new DirectoryInfo(x)))
            {
                AddLocalContentParts(package, manifest, layout, baseDirectory, Path.Combine(relativeDirectory, subDirectory.Name));
            }
        }

        void AddContent(Package package, PackageDefinition manifest, Uri partUri, string file)
        {
            var part =
                package.CreatePart(
                    PackUriHelper.CreatePartUri(partUri), System.Net.Mime.MediaTypeNames.Application.Octet, CompressionOption.Maximum);

            using (var partStream = part.GetStream())
            using (var fileStream = fileSystem.OpenFile(file, FileMode.Open))
            {
                fileStream.CopyTo(partStream);
                partStream.Flush();
                fileStream.Position = 0;
                var hashAlgorithm = new SHA256Managed();
                hashAlgorithm.ComputeHash(fileStream);
                manifest.Contents.Add(new ContentDefinition
                {
                    Name = partUri.ToString(),
                    Description =
                        new ContentDescription
                        {
                            DataStorePath = partUri,
                            LengthInBytes = (int) fileStream.Length,
                            HashAlgorithm = IntegrityCheckHashAlgorithm.Sha256,
                            Hash = Convert.ToBase64String(hashAlgorithm.Hash)
                        }
                });
            }
        }
    }
}