﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
 using Newtonsoft.Json;

namespace Calamari.Kubernetes.Conventions
{
    public class HelmUpgradeConvention: IInstallConvention
    {
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;
        readonly ICalamariFileSystem fileSystem;

        public HelmUpgradeConvention(IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, ICalamariFileSystem fileSystem)
        {
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
        }
        
        public void Install(RunningDeployment deployment)
        {
            var cmd = BuildHelmCommand(deployment);   
            var fileName = SyntaxSpecificFileName(deployment);
            
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

        string BuildHelmCommand(RunningDeployment deployment)
        {
            var releaseName = GetReleaseName(deployment.Variables);
            var packagePath = GetChartLocation(deployment);
            
            var sb = new StringBuilder();

            SetExecutable(deployment, sb);
            sb.Append($" upgrade --install");
            SetNamespaceParameter(deployment, sb);
            SetResetValuesParameter(deployment, sb);
            SetTillerTimeoutParameter(deployment, sb);
            SetTillerNamespaceParameter(deployment, sb);
            SetTimeoutParameter(deployment, sb);
            SetValuesParameters(deployment, sb);
            SetAdditionalArguments(deployment, sb);
            sb.Append($" \"{releaseName}\" \"{packagePath}\"");

            Log.Verbose(sb.ToString());
            return sb.ToString();
        }

        void SetExecutable(RunningDeployment deployment, StringBuilder sb)
        {
            var helmExecutable = deployment.Variables.Get(SpecialVariables.Helm.CustomHelmExecutable);
            if (!string.IsNullOrWhiteSpace(helmExecutable))
            {
                if (deployment.Variables.GetIndexes(Deployment.SpecialVariables.Packages.PackageCollection)
                        .Contains(SpecialVariables.Helm.Packages.CustomHelmExePackageKey) && !Path.IsPathRooted(helmExecutable))
                {
                    helmExecutable = Path.Combine(SpecialVariables.Helm.Packages.CustomHelmExePackageKey, helmExecutable);
                    Log.Info(
                        $"Using custom helm executable at {helmExecutable} from inside package. Full path at {Path.GetFullPath(helmExecutable)}");
                }
                else
                {
                    Log.Info($"Using custom helm executable at {helmExecutable}");
                }

                var scriptType = scriptEngine.GetSupportedTypes();
                if (scriptType.Contains(ScriptSyntax.PowerShell))
                {
                    sb.Append(". "); //With powershell we need to invoke custom executables
                }
                else
                {
                    sb.Append($"chmod +x \"{helmExecutable}\"\n");
                }

                sb.Append($"\"{helmExecutable}\"");
            }
            else
            {
                sb.Append("helm");
            }
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

        string SyntaxSpecificFileName(RunningDeployment deployment)
        {
            var scriptType = ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment();
            return Path.Combine(deployment.CurrentDirectory, scriptType == ScriptSyntax.PowerShell ? "Calamari.HelmUpgrade.ps1" : "Calamari.HelmUpgrade.sh");
        }

        static string GetReleaseName(CalamariVariableDictionary variables)
        {
            var validChars = new Regex("[^a-zA-Z0-9-]");
            var releaseName = variables.Get(SpecialVariables.Helm.ReleaseName)?.ToLower();
            if (string.IsNullOrWhiteSpace(releaseName))
            {
                releaseName = $"{variables.Get(Deployment.SpecialVariables.Action.Name)}-{variables.Get(Deployment.SpecialVariables.Environment.Name)}";
                releaseName = validChars.Replace(releaseName, "").ToLowerInvariant();
            }

            Log.SetOutputVariable("ReleaseName", releaseName, variables);
            Log.Info($"Using Release Name {releaseName}");
            return releaseName;
        }

        IEnumerable<string> AdditionalValuesFiles(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var packageReferenceNames = variables.GetIndexes(Deployment.SpecialVariables.Packages.PackageCollection);
            foreach (var packageReferenceName in packageReferenceNames)
            {
                var sanitizedPackageReferenceName = fileSystem.RemoveInvalidFileNameChars(packageReferenceName);
                var paths = variables.GetPaths(SpecialVariables.Helm.Packages.ValuesFilePath(packageReferenceName));
                
                foreach (var providedPath in paths)
                {
                    var packageId = variables.Get(Deployment.SpecialVariables.Packages.PackageId(packageReferenceName));
                    var version = variables.Get(Deployment.SpecialVariables.Packages.PackageVersion(packageReferenceName));
                    var relativePath = Path.Combine(sanitizedPackageReferenceName, providedPath);
                    var files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, relativePath).ToList();

                    if (!files.Any() && string.IsNullOrEmpty(packageReferenceName)) // Chart archives have chart name root directory 
                    {
                        Log.Verbose($"Unable to find values files at path `{providedPath}`. " +
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
                        Log.Info($"Including values file `{relative}` from package {packageId} v{version}");
                        yield return Path.GetFullPath(file);
                    }
                }
            }
        }

        string GetChartLocation(RunningDeployment deployment)
        {
            var packagePath = deployment.Variables.Get(Deployment.SpecialVariables.Package.Output.InstallationDirectoryPath);
            
            var packageId = deployment.Variables.Get(Deployment.SpecialVariables.Package.NuGetPackageId);

            if (fileSystem.FileExists(Path.Combine(packagePath, "Chart.yaml")))
            {
                return Path.Combine(packagePath, "Chart.yaml");
            }

            packagePath = Path.Combine(packagePath, packageId);
            if (!fileSystem.DirectoryExists(packagePath) || !fileSystem.FileExists(Path.Combine(packagePath, "Chart.yaml")))
            {
                throw new CommandException($"Unexpected error. Chart.yaml was not found in {packagePath}");
            }

            return packagePath;
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
    }
}