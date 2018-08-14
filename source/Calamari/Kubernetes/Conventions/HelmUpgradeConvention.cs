﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
 using Calamari.Shared;
 using Calamari.Shared.Commands;
 using Calamari.Shared.FileSystem;
 using Calamari.Shared.Scripting;
 using Calamari.Util;
using Newtonsoft.Json;
using Octostache;

namespace Calamari.Kubernetes.Conventions
{
    public class HelmUpgradeConvention: IConvention
    {
        private readonly IScriptRunner scriptEngine;
        private readonly ICalamariFileSystem fileSystem;
        private readonly ILog log;

        public HelmUpgradeConvention(IScriptRunner scriptEngine, ICalamariFileSystem fileSystem, ILog log)
        {
            this.scriptEngine = scriptEngine;
            this.fileSystem = fileSystem;
            this.log = log;
        }
        
        public void Run(IExecutionContext deployment)
        {
            if (string.IsNullOrEmpty(deployment.Variables.Get(SpecialVariables.ClusterUrl)))
            {
                throw new CommandException($"The variable `{SpecialVariables.ClusterUrl}` is not provided.");
            }

            var releaseName = GetReleaseName(deployment.Variables);

            var packagePath = GetChartLocation(deployment);

            var sb = new StringBuilder($"helm upgrade"); //Force reset to use values now in this release

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
            
            log.Verbose(sb.ToString());
            var fileName = GetFileName(deployment);
            using (new TemporaryFile(fileSystem, fileName))
            {
                fileSystem.OverwriteFile(fileName, sb.ToString());
                
                var result = scriptEngine.Execute(new Script(fileName));
                if (result.ExitCode != 0)
                {
                    throw new CommandException(string.Format(
                        "Helm Upgrade returned non-zero exit code: {0}. Deployment terminated.", result.ExitCode));
                }

                if (result.HasErrors &&
                    deployment.Variables.GetFlag(Shared.SpecialVariables.Action.FailScriptOnErrorOutput, false))
                {
                    throw new CommandException(
                        $"Helm Upgrade returned zero exit code but had error output. Deployment terminated.");
                }
            }
        }

        private string GetFileName(IExecutionContext deployment)
        {
            var scriptType = scriptEngine.GetSupportedTypes();
            if (scriptType.Contains(ScriptSyntax.PowerShell))
            {
                return Path.Combine(deployment.CurrentDirectory, "Calamari.HelmUpgrade.ps1");
            }
            
            return Path.Combine(deployment.CurrentDirectory, "Calamari.HelmUpgrade.sh");
        }

        private string GetReleaseName(VariableDictionary variables)
        {
            var validChars = new Regex("[^a-zA-Z0-9-]");
            var releaseName = variables.Get(SpecialVariables.Helm.ReleaseName)?.ToLower();
            if (string.IsNullOrWhiteSpace(releaseName))
            {
                releaseName = $"{variables.Get(Shared.SpecialVariables.Action.Name)}-{variables.Get(Shared.SpecialVariables.Environment.Name)}";
                releaseName = validChars.Replace(releaseName, "").ToLowerInvariant();
            }

            log.SetOutputVariable("ReleaseName", releaseName, variables);
            log.Info($"Using Release Name {releaseName}");
            return releaseName;
        }


        private IEnumerable<string> AdditionalValuesFiles(IExecutionContext deployment)
        {
            var variables = deployment.Variables;
            var packageReferenceNames = variables.GetIndexes(Shared.SpecialVariables.Packages.PackageCollection);
            foreach (var packageReferenceName in packageReferenceNames)
            {
                var sanitizedPackageReferenceName = fileSystem.RemoveInvalidFileNameChars(packageReferenceName);
                var paths = variables.GetPaths(SpecialVariables.Helm.Packages.ValuesFilePath(packageReferenceName));
                
                foreach (var providedPath in paths)
                {
                    var packageId = variables.Get(Shared.SpecialVariables.Packages.PackageId(packageReferenceName));
                    var version = variables.Get(Shared.SpecialVariables.Packages.PackageVersion(packageReferenceName));
                    var relativePath = Path.Combine(sanitizedPackageReferenceName, providedPath);
                    var files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, relativePath).ToList();
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

        private string GetChartLocation(IExecutionContext deployment)
        {
            var packagePath = deployment.Variables.Get(Shared.SpecialVariables.Package.Output.InstallationDirectoryPath);
            
            var packageId = deployment.Variables.Get(Shared.SpecialVariables.Package.NuGetPackageId);

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

        private static bool TryAddRawValuesYaml(IExecutionContext deployment, out string fileName)
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
        
        private static bool TryGenerateVariablesFile(IExecutionContext deployment, out string fileName)
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