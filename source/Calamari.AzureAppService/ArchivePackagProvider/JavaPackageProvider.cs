using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.Java;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureAppService
{
    public class JavaPackageProvider : IPackageProvider
    {
        readonly ICalamariFileSystem fileSystem;
        public bool SupportsAsynchronousDeployment => false;
        private ILog Log { get; }
        private IVariables Variables { get; }
        private RunningDeployment Deployment { get; }
        public string UploadUrlPath { get; }

        public JavaPackageProvider(ILog log, ICalamariFileSystem fileSystem, IVariables variables, RunningDeployment deployment, string uploadUrlPath)
        {
            this.fileSystem = fileSystem;
            Log = log;
            Variables = variables;
            Deployment = deployment;
            UploadUrlPath = uploadUrlPath;
        }

        public async Task<FileInfo> PackageArchive(string sourceDirectory, string targetDirectory)
        {
            var cmdLineRunner = new CommandLineRunner(Log, Variables);
            var jarTool = new JarTool(cmdLineRunner, Log, fileSystem, Variables);

            var packageMetadata = PackageName.FromFile(Deployment.PackageFilePath);

            var customPackageFileName = Variables.Get(PackageVariables.CustomPackageFileName);

            if (!string.IsNullOrWhiteSpace(customPackageFileName))
            {
                Log.Verbose($"Using custom package file-name: '{customPackageFileName}'");
            }

            var targetFilePath = Path.Combine(targetDirectory, customPackageFileName ?? Path.GetFileName(Deployment.PackageFilePath));

            var enableCompression = Variables.GetFlag(PackageVariables.JavaArchiveCompression, true);

            await Task.Run(() =>
            {
                jarTool.CreateJar(sourceDirectory, targetFilePath, enableCompression);
            });

            return new FileInfo(targetFilePath);
        }

        public async Task<FileInfo> ConvertToAzureSupportedFile(FileInfo sourceFile) => await Task.Run(() => sourceFile);

    }
}