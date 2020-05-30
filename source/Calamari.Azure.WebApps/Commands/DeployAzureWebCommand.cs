using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            var deployment = new RunningDeployment(pathToPackage, variables);

            extractPackage.ExtractToStagingDirectory(pathToPackage);
            configuredScriptService.Install(deployment, DeploymentStages.PreDeploy);
            packagedScriptService.Install(deployment, DeploymentStages.PreDeploy);
            substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(deployment);
            var appliedAsTransform = new ConfigurationTransformsService(fileSystem, configurationTransformer, transformFileLocator).Install(deployment);
            new ConfigurationVariablesService(fileSystem, replacer).Install(deployment, appliedAsTransform.ToList());
            new JsonConfigurationVariablesService(jsonReplacer, fileSystem).Install(deployment);
            packagedScriptService.Install(deployment, DeploymentStages.Deploy);
            configuredScriptService.Install(deployment, DeploymentStages.Deploy);
            new AzureWebAppService(log).Install(deployment);
            new LogAzureWebAppService(log).Install(deployment);
            packagedScriptService.Install(deployment, DeploymentStages.PostDeploy);
            configuredScriptService.Install(deployment, DeploymentStages.PostDeploy);

            return 0;
        }
    }
}