using System.IO;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace Calamari.Java.Deployment.Conventions
{
    public class RePackArchiveConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IPackageExtractor packageExtractor;

        public RePackArchiveConvention(ICalamariFileSystem fileSystem, IPackageExtractor packageExtractor)
        {
            this.fileSystem = fileSystem;
            this.packageExtractor = packageExtractor;
        }

        public void Install(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Action.Java.JavaArchiveExtractionDisabled, false))
                return;

            var packageMetadata = packageExtractor.GetMetadata(deployment.PackageFilePath);
            var applicationDirectory = ApplicationDirectory.GetApplicationDirectory(packageMetadata, deployment.Variables, fileSystem);
            var targetFilePath = Path.Combine(applicationDirectory,
                $"{packageMetadata.Id}.{packageMetadata.Version}.{Path.GetExtension(deployment.PackageFilePath)}");

            Log.Info($"Re-packaging archive: '{targetFilePath}'");
            var stagingDirectory = deployment.CurrentDirectory;


            using (var archive = ZipArchive.Create())
            {
                archive.AddAllFromDirectory(stagingDirectory);
                // We may want to read the compression-type of the original jar and match it?
                archive.SaveTo(targetFilePath, new WriterOptions(CompressionType.Deflate));
            }
        }
    }
}