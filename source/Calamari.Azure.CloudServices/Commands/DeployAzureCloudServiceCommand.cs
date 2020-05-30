using System.Collections.Generic;
using System.IO;
using Calamari.Azure.CloudServices.Accounts;
using Calamari.Azure.CloudServices.Deployment.Conventions;
using Calamari.Azure.CloudServices.Integration;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Features.Scripting;
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
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;

namespace Calamari.Azure.CloudServices.Commands
{
    [Command("deploy-azure-cloud-service", Description = "Extracts and installs an Azure Cloud-Service")]
    public class DeployAzureCloudServiceCommand : Command
    {
        private PathToPackage pathToPackage;
        readonly ILog log;
        private readonly IScriptEngine scriptEngine;
        readonly IVariables variables;
        readonly ICommandLineRunner commandLineRunner;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IExtractPackage extractPackage;

        public DeployAzureCloudServiceCommand(
            ILog log,
            IScriptEngine scriptEngine,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage
        )
        {
            Options.Add("package=", "Path to the NuGet package to install.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));

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

            Guard.NotNullOrWhiteSpace(pathToPackage, "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(pathToPackage))
                throw new CommandException("Could not find package file: " + pathToPackage);

            Log.Info("Deploying package:    " + pathToPackage);

            var account = new AzureAccount(variables);

            var fileSystem = new WindowsPhysicalFileSystem();
            var embeddedResources = new AssemblyEmbeddedResources();
            var azurePackageUploader = new AzurePackageUploader(log);
            var certificateStore = new CalamariCertificateStore();
            var cloudCredentialsFactory = new SubscriptionCloudCredentialsFactory(certificateStore);
            var cloudServiceConfigurationRetriever = new AzureCloudServiceConfigurationRetriever();
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
            var transformFileLocator = new TransformFileLocator(fileSystem);
            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var jsonVariablesReplacer = new JsonConfigurationVariableReplacer();
var packagedScriptService = new PackagedScriptService(log, fileSystem, scriptEngine, commandLineRunner);
var configuredScriptService = new ConfiguredScriptService(fileSystem, scriptEngine, commandLineRunner);

            var conventions = new List<IConvention>
            {
                new SwapAzureDeploymentConvention(fileSystem, embeddedResources, scriptEngine, commandLineRunner),
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage)),
                new FindCloudServicePackageConvention(fileSystem),
                new EnsureCloudServicePackageIsCtpFormatConvention(fileSystem),
                new ExtractAzureCloudServicePackageConvention(log, fileSystem),
                new ChooseCloudServiceConfigurationFileConvention(fileSystem),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.PreDeploy),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.PreDeploy),
                new ConfigureAzureCloudServiceConvention(account, fileSystem, cloudCredentialsFactory, cloudServiceConfigurationRetriever, certificateStore),
                new DelegateInstallConvention(d => substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(d)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsService(fileSystem, configurationTransformer, transformFileLocator)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesService(fileSystem, replacer)),
                new JsonConfigurationVariablesConvention(new JsonConfigurationVariablesService(jsonVariablesReplacer, fileSystem)),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.Deploy),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.Deploy),
                new RePackageCloudServiceConvention(fileSystem),
                new UploadAzureCloudServicePackageConvention(fileSystem, azurePackageUploader, cloudCredentialsFactory),
                new DeployAzureCloudServicePackageConvention(fileSystem, embeddedResources, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.PostDeploy),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.PostDeploy)
            };

            var deployment = new RunningDeployment(pathToPackage, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);
            conventionRunner.RunConventions();

            return 0;
        }
    }
}