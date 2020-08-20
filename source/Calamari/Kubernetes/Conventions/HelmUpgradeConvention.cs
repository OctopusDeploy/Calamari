﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Util;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.Conventions
{
    public class HelmUpgradeConvention : IInstallConvention
    {
        readonly ILog log;
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;
        readonly ICalamariFileSystem fileSystem;

        public HelmUpgradeConvention(ILog log, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
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
                    throw new CommandException(string.Format(
                        "Helm Upgrade returned non-zero exit code: {0}. Deployment terminated.", result.ExitCode));
                }

                if (result.HasErrors &&
                    deployment.Variables.GetFlag(Deployment.SpecialVariables.Action.FailScriptOnErrorOutput, false))
                {
                    throw new CommandException(
                        $"Helm Upgrade returned zero exit code but had error output. Deployment terminated.");
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
                        .Contains(SpecialVariables.Helm.Packages.CustomHelmExePackageKey) && !Path.IsPathRooted(helmExecutable))
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
            if (!int.TryParse(timeout, out _))
            {
                throw new CommandException($"Timeout period is not a valid integer: {timeout}");
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
            foreach (var packageReferenceName in packageReferenceNames)
            {
                var sanitizedPackageReferenceName = fileSystem.RemoveInvalidFileNameChars(packageReferenceName);
                var paths = variables.GetPaths(SpecialVariables.Helm.Packages.ValuesFilePath(packageReferenceName));
                
                foreach (var providedPath in paths)
                {
                    var packageId = variables.Get(PackageVariables.IndexedPackageId(packageReferenceName));
                    var version = variables.Get(PackageVariables.IndexedPackageVersion(packageReferenceName));
                    var relativePath = Path.Combine(sanitizedPackageReferenceName, providedPath);
                    var files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, relativePath).ToList();

                    if (!files.Any() && string.IsNullOrEmpty(packageReferenceName)) // Chart archives have chart name root directory 
                    {
                        log.Verbose($"Unable to find values files at path `{providedPath}`. " +
                                    $"Chart package contains root directory with chart name, so looking for values in there.");
                        var chartRelativePath = Path.Combine(fileSystem.RemoveInvalidFileNameChars(packageId), relativePath);
                        files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, chartRelativePath).ToList();
                    }

                    if (!files.Any())
                    {
                        throw new CommandException($"Unable to find file `{providedPath}` for package {packageId} v{version}");
                    }
                    
                    foreach (var file in files)
                    {
                        var relative = file.Substring(Path.Combine(deployment.CurrentDirectory, sanitizedPackageReferenceName).Length);
                        log.Info($"Including values file `{relative}` from package {packageId} v{version}");
                        yield return Path.GetFullPath(file);
                    }
                }
            }
        }

        string GetChartLocation(RunningDeployment deployment)
        {
            var installDir = deployment.Variables.Get(PackageVariables.Output.InstallationDirectoryPath);

            var packageId = deployment.Variables.Get(PackageVariables.IndexedPackageId(string.Empty));

            // Try the root directory
            if (fileSystem.FileExists(Path.Combine(installDir, "Chart.yaml")))
            {
                return Path.Combine(installDir, "Chart.yaml");
            }

            // Try the directory that matches the package id
            var packageIdPath = Path.Combine(installDir, packageId);
            if (fileSystem.DirectoryExists(packageIdPath) && fileSystem.FileExists(Path.Combine(packageIdPath, "Chart.yaml")))
            {
                return packageIdPath;
                
            }
            
            /*
             * Although conventions suggests that the directory inside the helm archive matches the package ID, this
             * can not be assumed. If the standard locations above failed to locate the Chart.yaml file, loop over
             * all subdirectories to try and find the file.
             */
            foreach (var dir in fileSystem.EnumerateDirectories(installDir))
            {
                if (fileSystem.FileExists(Path.Combine(dir, "Chart.yaml")))
                {
                    return dir;
                }
            }
            
            // Nothing worked
            throw new CommandException($"Unexpected error. Chart.yaml was not found in {packageIdPath}");
        }

        static bool TryAddRawValuesYaml(RunningDeployment deployment, out string fileName)
        {
            fileName = null;
            var yaml = deployment.Variables.Get(SpecialVariables.Helm.YamlValues);
            if (!string.IsNullOrWhiteSpace(yaml))
            {
                fileName = Path.Combine(deployment.CurrentDirectory, "rawYamlValues.yaml");
                File.WriteAllText(fileName, yaml);
                return true;
            }

            return false;
        }

        static bool TryGenerateVariablesFile(RunningDeployment deployment, out string fileName)
        {
            fileName = null;
            var variables = deployment.Variables.Get(SpecialVariables.Helm.KeyValues, "{}");
            var values = JsonConvert.DeserializeObject<Dictionary<string, object>>(variables);

            if (!values.Any())
            {
                return false;
            }
            
            fileName = Path.Combine(deployment.CurrentDirectory, "explicitVariableValues.yaml");
            File.WriteAllText(fileName, RawValuesToYamlConverter.Convert(values));
            return true;
        }
        
        void CheckHelmToolVersion(string customHelmExecutable, HelmVersion selectedVersion)
        {
            log.Verbose($"Helm version selected: {selectedVersion}");

            StringBuilder stdout = new StringBuilder();
            var result = SilentProcessRunner.ExecuteCommand(customHelmExecutable ?? "helm", "version --client --short", Environment.CurrentDirectory, output => stdout.Append(output), error => { });

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