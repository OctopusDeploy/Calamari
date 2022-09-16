using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Microsoft.WindowsAzure.Packaging;

namespace Calamari.AzureCloudService
{
    public class EnsureCloudServicePackageIsCtpFormatBehaviour : IAfterPackageExtractionBehaviour
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;

        public EnsureCloudServicePackageIsCtpFormatBehaviour(ILog log, ICalamariFileSystem fileSystem)
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
            log.VerboseFormat("Ensuring cloud-service-package is {0} format.", PackageFormats.V20120315.ToString());
            var packagePath = context.Variables.Get(SpecialVariables.Action.Azure.CloudServicePackagePath);
            var packageFormat = PackageConverter.GetFormat(packagePath);

            switch (packageFormat)
            {
                case PackageFormats.Legacy:
                    log.VerboseFormat("Package is Legacy format. Converting to {0} format.", PackageFormats.V20120315.ToString());
                    ConvertPackage(packagePath);
                    break;
                case PackageFormats.V20120315:
                    log.VerboseFormat("Package is {0} format.", PackageFormats.V20120315.ToString());
                    break;
                default:
                    throw new InvalidOperationException("Unexpected PackageFormat: " + packageFormat);
            }

            return this.CompletedTask();
        }

        void ConvertPackage(string packagePath)
        {
            var newPackagePath = Path.Combine(Path.GetDirectoryName(packagePath), Path.GetFileNameWithoutExtension(packagePath) + "_new.cspkg");
            using (var packageStore = new OpcPackageStore(newPackagePath, FileMode.CreateNew, FileAccess.ReadWrite))
            using (var fileStream = fileSystem.OpenFile(packagePath, FileMode.Open))
            {
                PackageConverter.ConvertFromLegacy(fileStream, packageStore);
            }

            fileSystem.OverwriteAndDelete(packagePath, newPackagePath);
        }
    }
}