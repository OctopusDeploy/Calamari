using System.IO;
using System.Text.RegularExpressions;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Kubernetes.Conventions
{
    public class HelmUpgradeConvention : IInstallConvention
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
            var syntax = ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment();
            var cmd = BuildHelmCommand(deployment, syntax);
            var fileName = SyntaxSpecificFileNameExtension("Calamari.HelmUpgrade", deployment, syntax);

            using (new TemporaryFile(fileName))
            {
                fileSystem.OverwriteFile(fileName, cmd);
                var result = scriptEngine.Execute(new Script(fileName), deployment.Variables, commandLineRunner);
                if (result.ExitCode != 0)
                    throw new CommandException($"Helm Upgrade returned non-zero exit code: {result.ExitCode}. Deployment terminated.");
                if (result.HasErrors && deployment.Variables.GetFlag(Deployment.SpecialVariables.Action.FailScriptOnErrorOutput, false))
                    throw new CommandException("Helm Upgrade returned zero exit code but had error output. Deployment terminated.");
            }
        }

        string BuildHelmCommand(RunningDeployment deployment, ScriptSyntax syntax)
        {
            var releaseName = GetReleaseName(deployment.Variables);
            var packagePath = GetChartLocation(deployment);

            //var tempDirectory = fileSystem.CreateTemporaryDirectory();
            var helmCommandBuilder = HelmBuilder.GetHelmCommandBuilderForInstalledHelmVersion(deployment.Variables, deployment.CurrentDirectory)
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

        static string SyntaxSpecificFileNameExtension(string fileName, RunningDeployment deployment, ScriptSyntax syntax)
        {
            return Path.Combine(deployment.CurrentDirectory, syntax == ScriptSyntax.PowerShell ? $"{fileName}.ps1" : $"{fileName}.sh");
        }

        static string GetReleaseName(CalamariVariableDictionary variables)
        {
            var releaseName = variables.Get(SpecialVariables.Helm.ReleaseName)?.ToLower();
            if (string.IsNullOrWhiteSpace(releaseName))
            {
                releaseName = $"{variables.Get(Deployment.SpecialVariables.Action.Name)}-{variables.Get(Deployment.SpecialVariables.Environment.Name)}";

                var validChars = new Regex("[^a-zA-Z0-9-]");
                releaseName = validChars.Replace(releaseName, "").ToLowerInvariant();
            }

            Log.SetOutputVariable("ReleaseName", releaseName, variables);
            Log.Info($"Using Release Name {releaseName}");
            return releaseName;
        }

        string GetChartLocation(RunningDeployment deployment)
        {
            var packagePath = deployment.Variables.Get(Deployment.SpecialVariables.Package.Output.InstallationDirectoryPath);
            var chartYamlPath = Path.Combine(packagePath, "Chart.yaml");
            if (fileSystem.FileExists(chartYamlPath))
                return chartYamlPath;

            var packageId = deployment.Variables.Get(Deployment.SpecialVariables.Package.NuGetPackageId);
            packagePath = Path.Combine(packagePath, packageId);
            if (!fileSystem.DirectoryExists(packagePath) || !fileSystem.FileExists(Path.Combine(packagePath, "Chart.yaml")))
                throw new CommandException($"Unexpected error. Chart.yaml was not found in {packagePath}");

            return packagePath;
        }
    }
}