using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes.Helm
{
    public static class PackageValuesFileWriter
    {
        public static IEnumerable<string> FindChartValuesFiles(RunningDeployment deployment, ICalamariFileSystem fileSystem, ILog log, string valuesFilePaths)
            => FindPackageValuesFiles(deployment,
                                      fileSystem,
                                      log,
                                      valuesFilePaths,
                                      string.Empty,
                                      string.Empty);

        public static IEnumerable<string> FindPackageValuesFiles(RunningDeployment deployment,
                                                                 ICalamariFileSystem fileSystem,
                                                                 ILog log,
                                                                 string valuesFilePaths,
                                                                 string packageId,
                                                                 string packageName)
        {
            var variables = deployment.Variables;
            var packageNames = variables.GetIndexes(PackageVariables.PackageCollection);
            if (!packageNames.Contains(packageName))
            {
                log?.Warn($"Failed to find variables for package {packageName}");
                return null;
            }

            //if the package name is null/empty then this is the chart primary package, so we don't need to validate the packageId
            if (!string.IsNullOrEmpty(packageName))
            {
                var packageIdFromVariables = variables.Get(PackageVariables.IndexedPackageId(packageName));
                if (string.IsNullOrEmpty(packageIdFromVariables) || !packageIdFromVariables.Equals(packageId, StringComparison.CurrentCultureIgnoreCase))
                {
                    return null;
                }
            }
            
            var valuesPaths = HelmValuesFileUtils.SplitValuesFilePaths(valuesFilePaths);
            if (valuesPaths == null || !valuesPaths.Any())
                return null;

            var filenames = new List<string>();
            var errors = new List<string>();

            var sanitizedPackageReferenceName = PackageName.ExtractPackageNameFromPathedPackageId(fileSystem.RemoveInvalidFileNameChars(packageName));
            
            var version = variables.Get(PackageVariables.IndexedPackageVersion(packageName));
            
            //we get the package id here
            var pathedPackedName = PackageName.ExtractPackageNameFromPathedPackageId(variables.Get(PackageVariables.IndexedPackageId(packageName)));
            foreach (var valuePath in valuesPaths)
            {
                var relativePath = Path.Combine(sanitizedPackageReferenceName, valuePath);
                var currentFiles = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, relativePath).ToList();

                if (!currentFiles.Any() && string.IsNullOrEmpty(packageName)) // Chart archives have chart name root directory
                {
                    log?.Verbose($"Unable to find values files at path `{valuePath}`. Chart package contains root directory with chart name, so looking for values in there.");
                    var chartRelativePath = Path.Combine(pathedPackedName, relativePath);
                    currentFiles = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, chartRelativePath).ToList();
                }

                if (!currentFiles.Any())
                {
                    errors.Add($"Unable to find file `{valuePath}` for package {pathedPackedName} v{version}");
                }

                foreach (var file in currentFiles)
                {
                    var relative = file.Substring(Path.Combine(deployment.CurrentDirectory, sanitizedPackageReferenceName).Length);
                    log?.Info($"Including values file `{relative}` from package {pathedPackedName} v{version}");
                    filenames.Add(file);
                }
            }

            if (!filenames.Any() && errors.Any())
            {
                throw new CommandException(string.Join(Environment.NewLine, errors));
            }

            return filenames;
        }
    }
}