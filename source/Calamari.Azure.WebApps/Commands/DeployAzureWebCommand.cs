using System.Collections.Generic;
using System.IO;
using Calamari.Azure.WebApps.Deployment.Conventions;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;

namespace Calamari.Azure.WebApps.Commands
{
    [Command("deploy-azure-web", Description = "Extracts and installs a deployment package to an Azure Web Application")]
    public class DeployAzureWebCommand : Command
    {
        private PathToPackage pathToPackage;
        readonly ILog log;
        private readonly IScriptEngine scriptEngine;
        readonly IVariables variables;
        readonly ICommandLineRunner commandLineRunner;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IExtractPackage extractPackage;

        public DeployAzureWebCommand(
            ILog log,
            IScriptEngine scriptEngine,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage
        )
        {
            Options.Add("package=", "Path to the deployment package to install.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));

            this.log = log;
            this.scriptEngine = scriptEngine;
            this.variables = variables;
            this.commandLineRunner = commandLineRunner;
            this.substituteInFiles = substituteInFiles;
            this.extractPackage = extractPackage;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(pathToPackage,
                "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(pathToPackage))
                throw new CommandException("Could not find package file: " + pathToPackage);

            Log.Info("Deploying package:    " + pathToPackage);

            var fileSystem = new WindowsPhysicalFileSystem();
            var replacer = new ConfigurationVariablesReplacer(variables, log);
            var structuredConfigVariableReplacer = new StructuredConfigVariableReplacer(new JsonFormatVariableReplacer(fileSystem, log), new YamlFormatVariableReplacer(), log);
            var transformFileLocator = new TransformFileLocator(fileSystem, log);
            var structuredConfigVariablesService = new StructuredConfigVariablesService(structuredConfigVariableReplacer, fileSystem, log);

            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage)),
                new ConfiguredScriptConvention(new ConfiguredScriptBehaviour(DeploymentStages.PreDeploy, log, fileSystem, scriptEngine, commandLineRunner)),
                new PackagedScriptConvention(new PackagedScriptBehaviour(log, DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner)),
                new DelegateInstallConvention(d => substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(d)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(fileSystem, ConfigurationTransformer.FromVariables(variables, log), transformFileLocator, log)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesBehaviour(fileSystem, replacer, log)),
                new JsonConfigurationVariablesConvention(new JsonConfigurationVariablesBehaviour(structuredConfigVariablesService)),
                new PackagedScriptConvention(new PackagedScriptBehaviour(log, DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner)),
                new ConfiguredScriptConvention(new ConfiguredScriptBehaviour(DeploymentStages.Deploy, log, fileSystem, scriptEngine, commandLineRunner)),
                new AzureWebAppConvention(log),
                new LogAzureWebAppDetails(log),
                new PackagedScriptConvention(new PackagedScriptBehaviour(log, DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner)),
                new ConfiguredScriptConvention(new ConfiguredScriptBehaviour(DeploymentStages.PostDeploy, log, fileSystem, scriptEngine, commandLineRunner)),
            };

            var deployment = new RunningDeployment(pathToPackage, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions, log);

            conventionRunner.RunConventions();

            return 0;
        }
    }
}