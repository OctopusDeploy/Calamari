using System.Collections.Generic;
using System.IO;
using Calamari.Azure.WebApps.Deployment.Conventions;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Features.Scripting;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;

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
            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var jsonReplacer = new JsonConfigurationVariableReplacer();
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
            var transformFileLocator = new TransformFileLocator(fileSystem);

            var packagedScriptService = new PackagedScriptService(log, fileSystem, scriptEngine, commandLineRunner);
            var configuredScriptService = new ConfiguredScriptService(fileSystem, scriptEngine, commandLineRunner);

            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage)),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.PreDeploy),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.PreDeploy),
                new DelegateInstallConvention(d => substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(d)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsService(fileSystem, configurationTransformer, transformFileLocator)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesService(fileSystem, replacer)),
                new JsonConfigurationVariablesConvention(new JsonConfigurationVariablesService(jsonReplacer, fileSystem)),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.Deploy),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.Deploy),
                new AzureWebAppConvention(new AzureWebAppService(log)),
                new LogAzureWebAppDetails(new LogAzureWebAppService(log)),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.PostDeploy),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.PostDeploy),
            };

            var deployment = new RunningDeployment(pathToPackage, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            conventionRunner.RunConventions();

            return 0;
        }
    }
}