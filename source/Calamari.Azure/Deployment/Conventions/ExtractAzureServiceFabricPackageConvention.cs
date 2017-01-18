using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;

namespace Calamari.Azure.Deployment.Conventions
{
    public class ExtractAzureServiceFabricPackageConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;

        public ExtractAzureServiceFabricPackageConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            var packagePath = deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServicePackagePath);
            Log.VerboseFormat("Extracting Azure Service Fabric package: '{0}'", packagePath);

            //TODO: markse - Consider the implications of package extraction to a hardcoded directory (ie. multiple SF apps in the one deployment working directory won't be able to run in parallel).
            //TODO: markse - Use the package name for this extraction path instead.

            var workingDirectory = deployment.CurrentDirectory;
            var destinationPath = Path.Combine(workingDirectory, "OctopusAzureServiceFabricPackage");
            using (var package = Package.Open(packagePath, FileMode.Open))
            {
                ExtractContents(package, destinationPath);
            }

            if (deployment.Variables.GetFlag(SpecialVariables.Action.Azure.FabricLogExtractedApplicationPackage))
                LogExtractedPackage(destinationPath);

            Log.SetOutputVariable(SpecialVariables.Action.Azure.FabricApplicationPackagePath,
                destinationPath,
                deployment.Variables);
        }

        #region Helpers

        void ExtractContents(Package package, string destinationPath)
        {
            ExtractPart(package.GetPart(
                PackUriHelper.ResolvePartUri(
                    new Uri("/", UriKind.Relative),
                    new Uri("/", UriKind.Relative))),
                destinationPath);
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

        void LogExtractedPackage(string workingDirectory)
        {
            Log.Verbose("Azure Service Fabric package extracted. Working directory contents:");
            LogDirectoryContents(workingDirectory, "", 0);
        }

        void LogDirectoryContents(string workingDirectory, string currentDirectoryRelativePath, int depth = 0)
        {
            var directory = new DirectoryInfo(Path.Combine(workingDirectory, currentDirectoryRelativePath));

            var files = fileSystem.EnumerateFiles(directory.FullName).ToList();
            for (int i = 0; i < files.Count; i++)
            {
                // Only log the first 50 files in each directory
                if (i == 50)
                {
                    Log.VerboseFormat("{0}And {1} more files...", Indent(depth), files.Count - i);
                    break;
                }

                var file = files[i];
                Log.Verbose(Indent(depth) + Path.GetFileName(file));
            }

            foreach (var subDirectory in fileSystem.EnumerateDirectories(directory.FullName).Select(x => new DirectoryInfo(x)))
            {
                Log.Verbose(Indent(depth + 1) + "\\" + subDirectory.Name);
                LogDirectoryContents(workingDirectory, Path.Combine(currentDirectoryRelativePath, subDirectory.Name), depth + 1);
            }
        }

        static string Indent(int n)
        {
            var indent = new StringBuilder("|");
            for (int i = 0; i < n; i++)
                indent.Append("-");

            return indent.ToString();
        }

        #endregion

    }
}
