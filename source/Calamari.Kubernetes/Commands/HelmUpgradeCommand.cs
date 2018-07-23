using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Certificates;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.Commands
{
    [Command("helm-upgrade", Description = "Performs Helm Upgrade with Chart while performing variable replacement")]
    public class HelmUpgradeCommand : Command
    {
        private string variablesFile;
        private string packageFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private readonly CombinedScriptEngine scriptEngine;

        public HelmUpgradeCommand(CombinedScriptEngine scriptEngine)
        {
            Options.Add("package=", "Path to the NuGet package to install.", v => packageFile = Path.GetFullPath(v));
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
            this.scriptEngine = scriptEngine;
        }
        
        public override int Execute(string[] commandLineArguments)
        {
              Options.Parse(commandLineArguments);

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);
            
            if (variablesFile != null && !File.Exists(variablesFile))
                throw new CommandException("Could not find variables file: " + variablesFile);

            Log.Info("Deploying package:    " + packageFile);
            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);

            var fileSystem = new WindowsPhysicalFileSystem();
            var embeddedResources = new AssemblyEmbeddedResources();
            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var certificateStore = new CalamariCertificateStore();
            var substituter = new FileSubstituter(fileSystem);
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
            var transformFileLocator = new TransformFileLocator(fileSystem);
            //var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var jsonVariablesReplacer = new JsonConfigurationVariableReplacer();

            //helm upgrade --namespace calamari-testing --install --reset-values --set SpecialMessage=Parameter  myrelease ./
            //--values newval.yaml
            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractorFactory().createStandardGenericPackageExtractor(), fileSystem),
                new SubstituteInFilesConvention(fileSystem, substituter),
                new HelmUpgradeConvention(scriptEngine, commandLineRunner, fileSystem)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);
            conventionRunner.RunConventions();

            return 0;
        }
    }

    
    
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
            var releaseName = deployment.Variables.Get(SpecialVariables.Helm.ReleaseName)?.ToLower();
            if (string.IsNullOrWhiteSpace(releaseName))
            {
                throw new CommandException("ReleaseName has not been set");
            }
            
            var packagePath = GetChartLocation(deployment);

            var sb = new StringBuilder($"helm upgrade --reset-values"); //Force reset to use values now in release
           
            if (deployment.Variables.GetFlag(SpecialVariables.Helm.Install, true))
            {
                sb.Append(" --install");
            }

            if (!TryGenerateVariablesFile(deployment, out var valuesFile))
            {
                sb.Append($" --values \"{valuesFile}\"");
            }

            sb.Append($" \"{releaseName}\" \"{packagePath}\"");
            
            
            var fileName = Path.Combine(fileSystem.CreateTemporaryDirectory(), "HelmUpgrade.ps1");
            using (new TemporaryFile(fileName))
            {
                fileSystem.OverwriteFile(fileName, sb.ToString());
                
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

        private string GetChartLocation(RunningDeployment deployment)
        {
            var packagePath = deployment.Variables.Get(Deployment.SpecialVariables.Package.Output.InstallationDirectoryPath);
            packagePath = Path.Combine(packagePath, "mychart");

            if (!fileSystem.DirectoryExists(packagePath) || !fileSystem.FileExists(Path.Combine(packagePath, "Chart.yaml")))
            {
                throw new CommandException($"Unexpected error. Chart.yaml was not found in {packagePath}");
            }

            return packagePath;
        }

        private static bool TryGenerateVariablesFile(RunningDeployment deployment, out string fileName)
        {
            fileName = null;
            var variables = deployment.Variables.Get(SpecialVariables.Helm.Variables, "{}");
            var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(variables);
            if (values.Keys.Any())
            {
                fileName = Path.Combine(deployment.CurrentDirectory, "newValues.yaml");
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