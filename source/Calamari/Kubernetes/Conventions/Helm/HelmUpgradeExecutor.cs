﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Helm;
using Calamari.Kubernetes.Integration;
using Calamari.Util;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.Conventions.Helm
{
    public class HelmUpgradeExecutor
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly HelmTemplateValueSourcesParser templateValueSourcesParser;
        readonly HelmCli helmCli;

        public HelmUpgradeExecutor(ILog log,
                                   ICalamariFileSystem fileSystem,
                                   HelmTemplateValueSourcesParser templateValueSourcesParser,
                                   HelmCli helmCli)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.templateValueSourcesParser = templateValueSourcesParser;
            this.helmCli = helmCli;
        }

        public void ExecuteHelmUpgrade(RunningDeployment deployment,
                                       string releaseName,
                                       CancellationTokenSource installCompletedCts,
                                       CancellationTokenSource installErrorCts)
        {
            var packagePath = GetChartLocation(deployment);

            var args = GetUpgradeCommandArgs(deployment);

            var result = helmCli.Upgrade(releaseName, packagePath, args);

            if (result.ExitCode != 0)
            {
                installErrorCts.Cancel();
                throw new CommandException($"Helm Upgrade returned non-zero exit code: {result.ExitCode}. Deployment terminated.");
            }

            if (result.HasErrors && deployment.Variables.GetFlag(Deployment.SpecialVariables.Action.FailScriptOnErrorOutput))
            {
                installErrorCts.Cancel();
                throw new CommandException("Helm Upgrade returned zero exit code but had error output. Deployment terminated.");
            }

            installCompletedCts.Cancel();
        }

        List<string> GetUpgradeCommandArgs(RunningDeployment deployment)
        {
            var args = new List<string>();

            AssertHelmV3();

            SetResetValuesParameter(deployment, args);
            SetTimeoutParameter(deployment, args);
            SetValuesParameters(deployment, args);
            var hasAdditionalArgs = SetAdditionalArguments(deployment, args);

            //Adjust args based on KOS
            if (deployment.Variables.GetFlag(SpecialVariables.ResourceStatusCheck))
            {
                AddKOSArgs(deployment.Variables, hasAdditionalArgs, args);
            }

            return args;
        }

        static void AddKOSArgs(IVariables variables, bool hasAdditionalArgs, List<string> args)
        {
            var additionalArgs = string.Empty;
            if (hasAdditionalArgs)
            {
                //the additional args are always the last in the list
                additionalArgs = args.Last();
            }

            //if wait for jobs is enabled in KOS and the helm flag is not set by the user, set it
            var waitForJobs = variables.GetFlag(SpecialVariables.WaitForJobs);
            if (!additionalArgs.Contains("--wait-for-jobs") && waitForJobs)
            {
                additionalArgs = $"{additionalArgs} --wait-for-jobs";
            }

            //if wait or atomic is not set, we need it for KOS, so just set wait
            //atomic is a superset of wait
            if (!additionalArgs.Contains("--wait") || !additionalArgs.Contains("--atomic"))
            {
                additionalArgs = $"{additionalArgs} --wait";
            }

            //update or add the additional args
            if (hasAdditionalArgs)
            {
                args[args.Count - 1] = additionalArgs;
            }
            else
            {
                args.Add(additionalArgs.Trim());
            }
        }

        string GetChartLocation(RunningDeployment deployment)
        {
            var installDir = deployment.Variables.Get(PackageVariables.Output.InstallationDirectoryPath);

            var chartDirectoryVariable = deployment.Variables.Get(SpecialVariables.Helm.ChartDirectory);

            // Try the specific chart directory if the variable has been set
            if (!string.IsNullOrEmpty(chartDirectoryVariable))
            {
                log.Verbose($"Attempting to find chart using configured directory '{chartDirectoryVariable}'");

                var chartDirectory = Path.Combine(installDir, chartDirectoryVariable);
                if (fileSystem.DirectoryExists(chartDirectory) && fileSystem.FileExists(Path.Combine(chartDirectory, "Chart.yaml")))
                {
                    log.Verbose($"Using chart found in configured directory '{chartDirectory}'");
                    return chartDirectory;
                }

                throw new CommandException($"Chart was not found in '{chartDirectoryVariable}'");
            }

            // Try the root directory
            log.Verbose($"Attempting to find chart in root of package installation directory '{installDir}'");

            if (fileSystem.FileExists(Path.Combine(installDir, "Chart.yaml")))
            {
                log.Verbose($"Using chart found at root of package installation directory '{installDir}'");
                return installDir;
            }

            var packageId = deployment.Variables.Get(PackageVariables.IndexedPackageId(string.Empty));

            if (!string.IsNullOrEmpty(packageId))
            {
                log.Verbose($"Attempting to find chart in directory based on package id '{packageId}'");

                // Try the directory that matches the package id
                var packageIdPath = Path.Combine(installDir, packageId);
                if (fileSystem.DirectoryExists(packageIdPath) && fileSystem.FileExists(Path.Combine(packageIdPath, "Chart.yaml")))
                {
                    log.Verbose($"Using chart found in directory based on package id '{packageIdPath}'");
                    return packageIdPath;
                }
            }

            /*
             * Although conventions suggests that the directory inside the helm archive matches the package ID, this
             * can not be assumed. If the standard locations above failed to locate the Chart.yaml file, loop over
             * all subdirectories to try and find the file.
             */
            log.Verbose($"Attempting to find chart in sub-directories of package");

            foreach (var dir in fileSystem.EnumerateDirectories(installDir))
            {
                if (fileSystem.FileExists(Path.Combine(dir, "Chart.yaml")))
                {
                    log.Verbose($"Using chart found in sub-directory '{dir}'");
                    return dir;
                }
            }

            // Nothing worked
            throw new CommandException($"Unexpected error. Chart.yaml was not found in any directories inside '{installDir}'");
        }

        static void SetResetValuesParameter(RunningDeployment deployment, List<string> args)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Helm.ResetValues, true))
            {
                args.Add("--reset-values");
            }
        }

        void SetValuesParameters(RunningDeployment deployment, List<string> args)
        {
            SetOrderedTemplateValues(deployment, args);

            //We leave the remaining values here as the users may not have migrated to the new structure
            foreach (var additionalValuesFile in AdditionalValuesFiles(deployment))
            {
                args.Add($"--values \"{additionalValuesFile}\"");
            }

            if (TryAddRawValuesYaml(deployment, out var rawValuesFile))
            {
                args.Add($"--values \"{rawValuesFile}\"");
            }

            if (TryGenerateVariablesFile(deployment, out var valuesFile))
            {
                args.Add($"--values \"{valuesFile}\"");
            }
        }

        bool TryAddRawValuesYaml(RunningDeployment deployment, out string fileName)
        {
            fileName = null;
            var yaml = deployment.Variables.Get(SpecialVariables.Helm.YamlValues);

            fileName = InlineYamlValuesFileWriter.WriteToFile(deployment, fileSystem, yaml);

            return fileName != null;
        }

        bool TryGenerateVariablesFile(RunningDeployment deployment, out string fileName)
        {
            fileName = null;
            var variables = deployment.Variables.Get(SpecialVariables.Helm.KeyValues, "{}");
            var values = JsonConvert.DeserializeObject<Dictionary<string, object>>(variables);

            fileName = KeyValuesValuesFileWriter.WriteToFile(deployment, fileSystem, values);

            return fileName != null;
        }

        void SetOrderedTemplateValues(RunningDeployment deployment, List<string> args)
        {
            var filenames = templateValueSourcesParser.ParseAndWriteTemplateValuesFilesFromAllSources(deployment);

            foreach (var filename in filenames)
            {
                args.Add($"--values \"{filename}\"");
            }
        }

        static bool SetAdditionalArguments(RunningDeployment deployment, List<string> args)
        {
            var additionalArguments = deployment.Variables.Get(SpecialVariables.Helm.AdditionalArguments);

            if (!string.IsNullOrWhiteSpace(additionalArguments))
            {
                args.Add(additionalArguments);
                return true;
            }

            return false;
        }

        static void SetTimeoutParameter(RunningDeployment deployment, List<string> args)
        {
            if (!deployment.Variables.IsSet(SpecialVariables.Helm.Timeout)) return;

            var timeout = deployment.Variables.Get(SpecialVariables.Helm.Timeout);

            if (!GoDurationParser.ValidateDuration(timeout))
            {
                throw new CommandException($"Timeout period is not a valid duration: {timeout}");
            }

            args.Add($"--timeout \"{timeout}\"");
        }

        IEnumerable<string> AdditionalValuesFiles(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var packageReferenceNames = variables.GetIndexes(PackageVariables.PackageCollection);

            List<string> files = new List<string>();
            List<string> errors = new List<string>();

            foreach (var packageReferenceName in packageReferenceNames)
            {
                var sanitizedPackageReferenceName = PackageName.ExtractPackageNameFromPathedPackageId(fileSystem.RemoveInvalidFileNameChars(packageReferenceName));
                var valuesPaths = variables.GetPaths(SpecialVariables.Helm.Packages.ValuesFilePath(sanitizedPackageReferenceName));
                foreach (var valuePath in valuesPaths)
                {
                    var packageId = PackageName.ExtractPackageNameFromPathedPackageId(variables.Get(PackageVariables.IndexedPackageId(packageReferenceName)));
                    var version = variables.Get(PackageVariables.IndexedPackageVersion(packageReferenceName));
                    var relativePath = Path.Combine(sanitizedPackageReferenceName, valuePath);
                    var currentFiles = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, relativePath).ToList();

                    if (!currentFiles.Any() && string.IsNullOrEmpty(packageReferenceName)) // Chart archives have chart name root directory
                    {
                        log.Verbose($"Unable to find values files at path `{valuePath}`. Chart package contains root directory with chart name, so looking for values in there.");
                        var chartRelativePath = Path.Combine(packageId, relativePath);
                        currentFiles = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, chartRelativePath).ToList();
                    }

                    if (!currentFiles.Any())
                    {
                        errors.Add($"Unable to find file `{valuePath}` for package {packageId} v{version}");
                    }

                    foreach (var file in currentFiles)
                    {
                        var relative = file.Substring(Path.Combine(deployment.CurrentDirectory, sanitizedPackageReferenceName).Length);
                        log.Info($"Including values file `{relative}` from package {packageId} v{version}");
                        files.Add(file);
                    }
                }
            }

            if (!files.Any() && errors.Any())
            {
                throw new CommandException(string.Join(Environment.NewLine, errors));
            }

            return files;
        }

        void AssertHelmV3()
        {
            var (exitCode, infoOutput) = helmCli.GetExecutableVersion();
            if (exitCode != 0)
            {
                log.Warn("Unable to retrieve the Helm tool version");
                return;
            }

            var toolVersion = HelmVersionParser.ParseVersion(infoOutput);
            if (!toolVersion.HasValue)
            {
                log.Warn("Unable to parse the Helm tool version text: " + infoOutput);
            }
            else if (toolVersion.Value != HelmVersion.V3)
            {
                throw new CommandException("Helm V2 is no longer supported. Please migrate to Helm V3.");
            }
        }
    }
}