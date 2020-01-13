using System.Collections.Generic;
using System.Diagnostics;
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

namespace Calamari.Kubernetes.Conventions
{
    public class HelmUpgradeConvention: IInstallConvention
    {
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;
        readonly ICalamariFileSystem fileSystem;
        readonly CommandCaptureOutput commandCaptureOutput;

        public HelmUpgradeConvention(IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner, CommandCaptureOutput commandCaptureOutput, ICalamariFileSystem fileSystem)
        {
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
            this.fileSystem = fileSystem;
            this.commandCaptureOutput = commandCaptureOutput;
        }
        
        public void Install(RunningDeployment deployment)
        {
            ScriptSyntax syntax = ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment();
            var cmd = BuildHelmCommand(deployment, syntax);   
            var fileName = SyntaxSpecificFileNameExtension("Calamari.HelmUpgrade", deployment, syntax);
            
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

            var helmCommandBuilder = HelmBuilder.GetHelmCommandBuilderForInstalledHelmVersion(fileSystem, deployment.Variables, deployment.CurrentDirectory)
                    .SetExecutable(deployment.Variables)
                    .WithCommand("upgrade")
                    .Install()
                    .NamespaceFromSpecialVariable(deployment)
                    .ResetValuesFromSpecialVariableFlag(deployment)
                    .TillerTimeoutFromSpecialVariable(deployment)
                    .TillerNamespaceFromSpecialVariable(deployment)
                    .TimeoutFromSpecialVariable(deployment)
                    .ValuesFromSpecialVariable(deployment, fileSystem)
                    .AdditionalArgumentsFromSpecialVariable(deployment)
                    .AdditionalArguments($" \"{releaseName}\" \"{packagePath}\"");

            Log.Verbose(helmCommandBuilder.Build());
            return helmCommandBuilder.Build();
        }

        string SyntaxSpecificFileNameExtension(string fileName, RunningDeployment deployment, ScriptSyntax syntax)
        {
            return Path.Combine(deployment.CurrentDirectory, syntax == ScriptSyntax.PowerShell ? $"{fileName}.ps1" : $"{fileName}.sh");
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
    }
}