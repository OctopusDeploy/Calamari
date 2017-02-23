using System.Collections.Generic;
using System.IO;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Azure.Integration;
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

namespace Calamari.Azure.Commands
{
    [Command("deploy-azure-cloud-service", Description = "Extracts and installs an Azure Cloud-Service")]
    public class DeployAzureCloudServiceCommand : Command
    {
        private string variablesFile;
        private string packageFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;

        public DeployAzureCloudServiceCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("package=", "Path to the NuGet package to install.", v => packageFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(packageFile, "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);    

            if (variablesFile != null && !File.Exists(variablesFile))
                throw new CommandException("Could not find variables file: " + variablesFile);

            Log.Info("Deploying package:    " + packageFile);
            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);

            var fileSystem = new WindowsPhysicalFileSystem();
            var embeddedResources = new AssemblyEmbeddedResources();
            var scriptEngine = new CombinedScriptEngine();
            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var azurePackageUploader = new AzurePackageUploader();
            var certificateStore = new CalamariCertificateStore();
            var cloudCredentialsFactory = new SubscriptionCloudCredentialsFactory(certificateStore);
            var cloudServiceConfigurationRetriever = new AzureCloudServiceConfigurationRetriever();
            var substituter = new FileSubstituter(fileSystem);
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
            var transformFileLocator = new TransformFileLocator(fileSystem);
            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var jsonVariablesReplacer = new JsonConfigurationVariableReplacer();

            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new SwapAzureDeploymentConvention(fileSystem, embeddedResources, scriptEngine, commandLineRunner),
                new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractor(), fileSystem),
                new FindCloudServicePackageConvention(fileSystem),
                new EnsureCloudServicePackageIsCtpFormatConvention(fileSystem),
                new ExtractAzureCloudServicePackageConvention(fileSystem),
                new ChooseCloudServiceConfigurationFileConvention(fileSystem),
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfigureAzureCloudServiceConvention(fileSystem, cloudCredentialsFactory, cloudServiceConfigurationRetriever),
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
                new ConfiguredScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner),
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);
            conventionRunner.RunConventions();

            return 0;
        }
    }
}
