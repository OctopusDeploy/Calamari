using System.Collections.Generic;
using System.IO;
using Calamari.Azure.CloudServices.Accounts;
using Calamari.Azure.CloudServices.Deployment.Conventions;
using Calamari.Azure.CloudServices.Integration;
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
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;

namespace Calamari.Azure.CloudServices.Commands
{
    [Command("deploy-azure-cloud-service", Description = "Extracts and installs an Azure Cloud-Service")]
    public class DeployAzureCloudServiceCommand : Command
    {
        private string packageFile;
        readonly ILog log;
        private readonly CombinedScriptEngine scriptEngine;
        readonly IVariables variables;
        readonly ICommandLineRunner commandLineRunner;

        public DeployAzureCloudServiceCommand(ILog log, CombinedScriptEngine scriptEngine, IVariables variables, ICommandLineRunner commandLineRunner)
        {
            Options.Add("package=", "Path to the NuGet package to install.", v => packageFile = Path.GetFullPath(v));

            this.log = log;
            this.scriptEngine = scriptEngine;
            this.variables = variables;
            this.commandLineRunner = commandLineRunner;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(packageFile, "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);    

            Log.Info("Deploying package:    " + packageFile);

            var account = new AzureAccount(variables);
            
            var fileSystem = new WindowsPhysicalFileSystem();
            var embeddedResources = new AssemblyEmbeddedResources();
            var azurePackageUploader = new AzurePackageUploader(log);
            var certificateStore = new CalamariCertificateStore();
            var cloudCredentialsFactory = new SubscriptionCloudCredentialsFactory(certificateStore);
            var cloudServiceConfigurationRetriever = new AzureCloudServiceConfigurationRetriever();
            var substituter = new FileSubstituter(log, fileSystem);
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
            var transformFileLocator = new TransformFileLocator(fileSystem);
            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var jsonVariablesReplacer = new JsonConfigurationVariableReplacer();

            var conventions = new List<IConvention>
            {
                new SwapAzureDeploymentConvention(fileSystem, embeddedResources, scriptEngine, commandLineRunner),
                new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractorFactory(log).CreateStandardGenericPackageExtractor(), fileSystem),
                new FindCloudServicePackageConvention(fileSystem),
                new EnsureCloudServicePackageIsCtpFormatConvention(fileSystem),
                new ExtractAzureCloudServicePackageConvention(log, fileSystem),
                new ChooseCloudServiceConfigurationFileConvention(fileSystem),
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfigureAzureCloudServiceConvention(account, fileSystem, cloudCredentialsFactory, cloudServiceConfigurationRetriever, certificateStore),
                new SubstituteInFilesConvention(fileSystem, substituter),
                new ConfigurationTransformsConvention(fileSystem, configurationTransformer, transformFileLocator),
                new ConfigurationVariablesConvention(fileSystem, replacer),
                new JsonConfigurationVariablesConvention(jsonVariablesReplacer, fileSystem),
                new PackagedScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new RePackageCloudServiceConvention(fileSystem),
                new UploadAzureCloudServicePackageConvention(fileSystem, azurePackageUploader, cloudCredentialsFactory),
                new DeployAzureCloudServicePackageConvention(fileSystem, embeddedResources, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);
            conventionRunner.RunConventions();

            return 0;
        }
    }
}
