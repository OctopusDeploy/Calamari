using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Helm;
using Calamari.Util;
using Newtonsoft.Json;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Kubernetes.Conventions
{
    public class HelmUpgradeConvention : IInstallConvention
    {
        readonly ILog log;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;
        readonly ICalamariFileSystem fileSystem;
        readonly HelmTemplateValueSourcesParser valueSourcesParser;

        public HelmUpgradeConvention(ILog log, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, ICalamariFileSystem fileSystem, HelmTemplateValueSourcesParser valueSourcesParser)
        {
            this.log = log;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
            this.valueSourcesParser = valueSourcesParser;
        }

        public void Install(RunningDeployment deployment)
        {
            ScriptSyntax syntax = ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment();
            var cmd = BuildHelmCommand(deployment, syntax);
            var fileName = SyntaxSpecificFileName(deployment, syntax);

            using (new TemporaryFile(fileName))
            {
                fileSystem.OverwriteFile(fileName, cmd);
                var result = scriptEngine.Execute(new Script(fileName), deployment.Variables, commandLineRunner);
                if (result.ExitCode != 0)
                {
                    throw new CommandException(
                                               $"Helm Upgrade returned non-zero exit code: {result.ExitCode}. Deployment terminated.");
                }

                if (result.HasErrors && deployment.Variables.GetFlag(Deployment.SpecialVariables.Action.FailScriptOnErrorOutput, false))
                {
                    throw new CommandException(
                                               "Helm Upgrade returned zero exit code but had error output. Deployment terminated.");
                }
            }
        }

        string BuildHelmCommand(RunningDeployment deployment, ScriptSyntax syntax)
        {
            var releaseName = GetReleaseName(deployment.Variables);
            var packagePath = GetChartLocation(deployment);

            var customHelmExecutable = CustomHelmExecutableFullPath(deployment.Variables, deployment.CurrentDirectory);
            var helmVersion = GetVersion(deployment.Variables);
            CheckHelmToolVersion(customHelmExecutable, helmVersion);

            if (helmVersion == HelmVersion.V2)
            {
                if (FeatureToggle.PreventHelmV2DeploymentsFeatureToggle.IsEnabled(deployment.Variables))
                {
                    throw new CommandException("Helm V2 is no longer supported. Please migrate to Helm V3.");
                }
                else
                {
                    log.Warn("This step is currently configured to use Helm V2. Support for Helm V2 will be completely removed in Octopus Server 2025.1. Please migrate to Helm V3 as soon as possible.");
                }
            }

            var sb = new StringBuilder();

            SetExecutable(sb, syntax, customHelmExecutable);
            sb.Append($" upgrade --install");
            SetNamespaceParameter(deployment, sb);
            SetResetValuesParameter(deployment, sb);
            if (helmVersion == HelmVersion.V2)
            {
                SetTillerTimeoutParameter(deployment, sb);
                SetTillerNamespaceParameter(deployment, sb);
            }

            SetTimeoutParameter(deployment, sb);
            SetValuesParameters(deployment, sb);
            SetAdditionalArguments(deployment, sb);
            sb.Append($" \"{releaseName}\" \"{packagePath}\"");

            log.Verbose(sb.ToString());
            return sb.ToString();
        }

        HelmVersion GetVersion(IVariables variables)
        {
            var clientVersionText = variables.Get(SpecialVariables.Helm.ClientVersion);

            if (Enum.TryParse(clientVersionText, out HelmVersion version))
                return version;

            throw new CommandException($"Unrecognized Helm version: '{clientVersionText}'");
        }

        void SetExecutable(StringBuilder sb, ScriptSyntax syntax, string customHelmExecutable)
        {
            if (customHelmExecutable != null)
            {
                // With PowerShell we need to invoke custom executables
                sb.Append(syntax == ScriptSyntax.PowerShell ? ". " : $"chmod +x \"{customHelmExecutable}\"\n");
                sb.Append($"\"{customHelmExecutable}\"");
            }
            else
            {
                sb.Append("helm");
            }
        }

        string CustomHelmExecutableFullPath(IVariables variables, string workingDirectory)
        {
            var helmExecutable = variables.Get(SpecialVariables.Helm.CustomHelmExecutable);
            if (!string.IsNullOrWhiteSpace(helmExecutable))
            {
                if (variables.GetIndexes(PackageVariables.PackageCollection)
                             .Contains(SpecialVariables.Helm.Packages.CustomHelmExePackageKey)
                    && !Path.IsPathRooted(helmExecutable))
                {
                    var fullPath = Path.GetFullPath(Path.Combine(workingDirectory, SpecialVariables.Helm.Packages.CustomHelmExePackageKey, helmExecutable));
                    log.Info(
                             $"Using custom helm executable at {helmExecutable} from inside package. Full path at {fullPath}");

                    return fullPath;
                }
                else
                {
                    log.Info($"Using custom helm executable at {helmExecutable}");
                    return helmExecutable;
                }
            }

            return null;
        }

        static void SetNamespaceParameter(RunningDeployment deployment, StringBuilder sb)
        {
            var @namespace = deployment.Variables.Get(SpecialVariables.Helm.Namespace);
            if (!string.IsNullOrWhiteSpace(@namespace))
            {
                sb.Append($" --namespace \"{@namespace}\"");
            }
        }

        static void SetResetValuesParameter(RunningDeployment deployment, StringBuilder sb)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Helm.ResetValues, true))
            {
                sb.Append(" --reset-values");
            }
        }

        void SetValuesParameters(RunningDeployment deployment, StringBuilder sb)
        {
            SetOrderedTemplateValues(deployment, sb);

            //We leave the remaining values here as the users may not have migrated to the new structure
            foreach (var additionalValuesFile in AdditionalValuesFiles(deployment))
            {
                sb.Append($" --values \"{additionalValuesFile}\"");
            }

            if (TryAddRawValuesYaml(deployment, out var rawValuesFile))
            {
                sb.Append($" --values \"{rawValuesFile}\"");
            }

            if (TryGenerateVariablesFile(deployment, out var valuesFile))
            {
                sb.Append($" --values \"{valuesFile}\"");
            }
        }

        void SetOrderedTemplateValues(RunningDeployment deployment, StringBuilder sb)
        {
            var filenames = valueSourcesParser.ParseAndWriteTemplateValuesFilesFromAllSources(deployment);

            foreach (var filename in filenames)
            {
                sb.Append($" --values \"{filename}\"");
            }
        }

        void SetAdditionalArguments(RunningDeployment deployment, StringBuilder sb)
        {
            var additionalArguments = deployment.Variables.Get(SpecialVariables.Helm.AdditionalArguments);

            if (!string.IsNullOrWhiteSpace(additionalArguments))
            {
                sb.Append($" {additionalArguments}");
            }
        }

        static void SetTillerNamespaceParameter(RunningDeployment deployment, StringBuilder sb)
        {
            if (deployment.Variables.IsSet(SpecialVariables.Helm.TillerNamespace))
            {
                sb.Append($" --tiller-namespace \"{deployment.Variables.Get(SpecialVariables.Helm.TillerNamespace)}\"");
            }
        }

        static void SetTimeoutParameter(RunningDeployment deployment, StringBuilder sb)
        {
            if (!deployment.Variables.IsSet(SpecialVariables.Helm.Timeout)) return;

            var timeout = deployment.Variables.Get(SpecialVariables.Helm.Timeout);

            if (!GoDurationParser.ValidateDuration(timeout))
            {
                throw new CommandException($"Timeout period is not a valid duration: {timeout}");
            }

            sb.Append($" --timeout \"{timeout}\"");
        }

        static void SetTillerTimeoutParameter(RunningDeployment deployment, StringBuilder sb)
        {
            if (!deployment.Variables.IsSet(SpecialVariables.Helm.TillerTimeout)) return;

            var tillerTimeout = deployment.Variables.Get(SpecialVariables.Helm.TillerTimeout);
            if (!int.TryParse(tillerTimeout, out _))
            {
                throw new CommandException($"Tiller timeout period is not a valid integer: {tillerTimeout}");
            }

            sb.Append($" --tiller-connection-timeout \"{tillerTimeout}\"");
        }

        string SyntaxSpecificFileName(RunningDeployment deployment, ScriptSyntax syntax)
        {
            return Path.Combine(deployment.CurrentDirectory, syntax == ScriptSyntax.PowerShell ? "Calamari.HelmUpgrade.ps1" : "Calamari.HelmUpgrade.sh");
        }

        string GetReleaseName(IVariables variables)
        {
            var validChars = new Regex("[^a-zA-Z0-9-]");
            var releaseName = variables.Get(SpecialVariables.Helm.ReleaseName)?.ToLower();
            if (string.IsNullOrWhiteSpace(releaseName))
            {
                releaseName = $"{variables.Get(ActionVariables.Name)}-{variables.Get(DeploymentEnvironment.Name)}";
                releaseName = validChars.Replace(releaseName, "").ToLowerInvariant();
            }

            log.SetOutputVariable("ReleaseName", releaseName, variables);
            log.Info($"Using Release Name {releaseName}");
            return releaseName;
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

        void CheckHelmToolVersion(string customHelmExecutable, HelmVersion selectedVersion)
        {
            log.Verbose($"Helm version selected: {selectedVersion}");

            StringBuilder stdout = new StringBuilder();
            var result = SilentProcessRunner.ExecuteCommand(customHelmExecutable ?? "helm",
                                                            "version --client --short",
                                                            Environment.CurrentDirectory,
                                                            output => stdout.Append(output),
                                                            error => { });

            if (result.ExitCode != 0)
                log.Warn("Unable to retrieve the Helm tool version");

            var toolVersion = HelmVersionParser.ParseVersion(stdout.ToString());
            if (!toolVersion.HasValue)
                log.Warn("Unable to parse the Helm tool version text: " + stdout);

            if (toolVersion.Value != selectedVersion)
                log.Warn($"The Helm tool version '{toolVersion.Value}' ('{stdout}') doesn't match the Helm version selected '{selectedVersion}'");
        }
    }
}