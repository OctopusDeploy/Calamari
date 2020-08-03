﻿using System;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Microsoft.WindowsAzure.Packaging;

namespace Calamari.Azure.CloudServices.Deployment.Conventions
{
    public class EnsureCloudServicePackageIsCtpFormatConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;

        public EnsureCloudServicePackageIsCtpFormatConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Action.Azure.CloudServicePackageExtractionDisabled, false))
                return;

            Log.VerboseFormat("Ensuring cloud-service-package is {0} format.", PackageFormats.V20120315.ToString());
            var packagePath = deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServicePackagePath);
            var packageFormat = PackageConverter.GetFormat(packagePath);

            switch (packageFormat)
            {
                case PackageFormats.Legacy:
                    Log.VerboseFormat("Package is Legacy format. Converting to {0} format.", PackageFormats.V20120315.ToString());
                    ConvertPackage(packagePath);
                    return;
                case PackageFormats.V20120315:
                    Log.VerboseFormat("Package is {0} format.", PackageFormats.V20120315.ToString());
                    return;
                default:
                    throw new InvalidOperationException("Unexpected PackageFormat: " + packageFormat);
            }
        }

        void ConvertPackage(string packagePath)
        {
            string newPackagePath = Path.Combine(Path.GetDirectoryName(packagePath), Path.GetFileNameWithoutExtension(packagePath) + "_new.cspkg");
            using (var packageStore = new OpcPackageStore(newPackagePath, FileMode.CreateNew, FileAccess.ReadWrite))
            using (var fileStream = fileSystem.OpenFile(packagePath, FileMode.Open))
            {
                PackageConverter.ConvertFromLegacy(fileStream, packageStore);
            }

            fileSystem.OverwriteAndDelete(packagePath, newPackagePath);
        }
    }
}