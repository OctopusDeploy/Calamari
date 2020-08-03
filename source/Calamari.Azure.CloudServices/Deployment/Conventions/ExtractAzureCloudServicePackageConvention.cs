﻿using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using Calamari.Azure.CloudServices.Integration.CloudServicePackage;
using Calamari.Azure.CloudServices.Integration.CloudServicePackage.ManifestSchema;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Util;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Azure.CloudServices.Deployment.Conventions
{
    public class ExtractAzureCloudServicePackageConvention : IInstallConvention
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;

        public ExtractAzureCloudServicePackageConvention(ILog log, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Action.Azure.CloudServicePackageExtractionDisabled, false))
                return;

            var packagePath = deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServicePackagePath);
            log.VerboseFormat("Extracting Cloud Service package: '{0}'", packagePath);
            using (var package = Package.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var manifest = AzureCloudServiceConventions.ReadPackageManifest(package);
                var workingDirectory = deployment.CurrentDirectory;

                ExtractContents(package, manifest, AzureCloudServiceConventions.PackageFolders.ServiceDefinition, workingDirectory);
                ExtractContents(package, manifest, AzureCloudServiceConventions.PackageFolders.NamedStreams, workingDirectory);
                ExtractLayouts(package, manifest, workingDirectory);
            }


            if (deployment.Variables.GetFlag(SpecialVariables.Action.Azure.LogExtractedCspkg))
                LogExtractedPackage(deployment.CurrentDirectory);

            log.SetOutputVariable(SpecialVariables.Action.Azure.PackageExtractionPath, deployment.CurrentDirectory, deployment.Variables);
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
            log.Verbose("CSPKG extracted. Working directory contents:");
            DirectoryLoggingHelper.LogDirectoryContents(log, fileSystem, workingDirectory, "", 0);
        }
    }
}