using System.Collections.Generic;
using System.IO;
using Calamari.Azure.ServiceFabric.Deployment.Conventions;
using Calamari.Azure.ServiceFabric.Util;
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

namespace Calamari.Azure.ServiceFabric.Commands
{
    [Command("deploy-azure-service-fabric-app", Description = "Extracts and installs an Azure Service Fabric Application")]
    public class DeployAzureServiceFabricAppCommand : Command
    {
        private string packageFile;
        private readonly CombinedScriptEngine scriptEngine;
        private readonly ICertificateStore certificateStore;
        readonly IVariables variables;

        public DeployAzureServiceFabricAppCommand(CombinedScriptEngine scriptEngine, ICertificateStore certificateStore, IVariables variables)
        {
            Options.Add("package=", "Path to the NuGet package to install.", v => packageFile = Path.GetFullPath(v));

            this.scriptEngine = scriptEngine;
            this.certificateStore = certificateStore;
            this.variables = variables;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (!ServiceFabricHelper.IsServiceFabricSdkKeyInRegistry())
                throw new CommandException("Could not find the Azure Service Fabric SDK on this server. This SDK is required before running Service Fabric commands.");

            Guard.NotNullOrWhiteSpace(packageFile,
                "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);

            Log.Info("Deploying package:    " + packageFile);

            var fileSystem = new WindowsPhysicalFileSystem();
            var embeddedResources = new AssemblyEmbeddedResources();
            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var jsonReplacer = new JsonConfigurationVariableReplacer();
            var substituter = new FileSubstituter(fileSystem);
            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
            var transformFileLocator = new TransformFileLocator(fileSystem);

            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new LogVariablesConvention(),
                new ExtractPackageToStagingDirectoryConvention(new GenericPackageExtractorFactory().createStandardGenericPackageExtractor(), fileSystem),

                // PreDeploy stage
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),

                // Standard variable and transform replacements
                new SubstituteInFilesConvention(fileSystem, substituter),
                new ConfigurationTransformsConvention(fileSystem, configurationTransformer, transformFileLocator),
                new ConfigurationVariablesConvention(fileSystem, replacer),
                new JsonConfigurationVariablesConvention(jsonReplacer, fileSystem),

                // Deploy stage
                new PackagedScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),

                // Variable replacement
                new SubstituteVariablesInAzureServiceFabricPackageConvention(fileSystem, substituter),

                // Main Service Fabric deployment script execution
                new EnsureCertificateInstalledInStoreConvention(certificateStore, SpecialVariables.Action.ServiceFabric.ClientCertVariable, SpecialVariables.Action.ServiceFabric.CertificateStoreLocation, SpecialVariables.Action.ServiceFabric.CertificateStoreName),
                new DeployAzureServiceFabricAppConvention(fileSystem, embeddedResources, scriptEngine, commandLineRunner),

                // PostDeploy stage
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
