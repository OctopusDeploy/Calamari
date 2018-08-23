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
using Calamari.Util;
using Newtonsoft.Json;
using Octostache;

namespace Calamari.Kubernetes.Conventions
{
    public class HelmUpgradeConvention: IInstallConvention
    {
        private readonly IScriptEngine scriptEngine;
        private readonly ICommandLineRunner commandLineRunner;
        private readonly ICalamariFileSystem fileSystem;

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

        private string BuildHelmCommand(RunningDeployment deployment)
        {
            var releaseName = GetReleaseName(deployment.Variables);
            var packagePath = GetChartLocation(deployment);
            
            var sb = new StringBuilder();
            
            var helmExecutable = deployment.Variables.Get(SpecialVariables.Helm.CustomHelmExecutable);
            if (!string.IsNullOrWhiteSpace(helmExecutable))
            {
                Log.Info($"Using custom helm executable at {helmExecutable}");
                if (deployment.Variables.GetIndexes(Deployment.SpecialVariables.Packages.PackageCollection)
                    .Contains(SpecialVariables.Helm.Packages.CustomHelmExePackageKey) && !Path.IsPathRooted(helmExecutable))
                {
                    helmExecutable = Path.Combine(SpecialVariables.Helm.Packages.CustomHelmExePackageKey, helmExecutable);
                    Log.Verbose($"Full helm executable path: {helmExecutable}");
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
            
            sb.Append($" upgrade");

            if (deployment.Variables.GetFlag(SpecialVariables.Helm.ResetValues, true))
            {
                sb.Append(" --reset-values");
            }

            /*if (deployment.Variables.GetFlag(SpecialVariables.Helm.Install, true))
            {*/
            sb.Append(" --install");
            /*}*/

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

            sb.Append($" \"{releaseName}\" \"{packagePath}\"");

            Log.Verbose(sb.ToString());
            return sb.ToString();
        }

        private string SyntaxSpecificFileName(RunningDeployment deployment)
        {
            var scriptType = scriptEngine.GetSupportedTypes();
            if (scriptType.Contains(ScriptSyntax.PowerShell))
            {
                return Path.Combine(deployment.CurrentDirectory, "Calamari.HelmUpgrade.ps1");
            }
            
            return Path.Combine(deployment.CurrentDirectory, "Calamari.HelmUpgrade.sh");
        }

        private static string GetReleaseName(CalamariVariableDictionary variables)
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

        private IEnumerable<string> AdditionalValuesFiles(RunningDeployment deployment)
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

        private string GetChartLocation(RunningDeployment deployment)
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

        private static bool TryAddRawValuesYaml(RunningDeployment deployment, out string fileName)
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
        
        private static bool TryGenerateVariablesFile(RunningDeployment deployment, out string fileName)
        {
            fileName = null;
            var variables = deployment.Variables.Get(SpecialVariables.Helm.KeyValues, "{}");
            var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(variables);
            if (values.Keys.Any())
            {
                fileName = Path.Combine(deployment.CurrentDirectory, "explicitVariableValues.yaml");
                using (var outputFile = new StreamWriter(fileName, false))
                {
                    foreach (var kvp in values)
                    {
                        outputFile.WriteLine($"{kvp.Key}: \"{kvp.Value}\"");
                    }
                }
                return true;
            }
            return false;
        }
    }
}