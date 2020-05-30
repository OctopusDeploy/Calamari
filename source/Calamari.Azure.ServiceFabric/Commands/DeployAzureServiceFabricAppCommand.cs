using System.Collections.Generic;
using System.IO;
using Calamari.Azure.ServiceFabric.Deployment.Conventions;
using Calamari.Azure.ServiceFabric.Util;
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

namespace Calamari.Azure.ServiceFabric.Commands
{
    [Command("deploy-azure-service-fabric-app", Description = "Extracts and installs an Azure Service Fabric Application")]
    public class DeployAzureServiceFabricAppCommand : Command
    {
        private PathToPackage pathToPackage;
        readonly ILog log;
        private readonly IScriptEngine scriptEngine;
        private readonly ICertificateStore certificateStore;
        readonly IVariables variables;
        readonly ICommandLineRunner commandLineRunner;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IFileSubstituter fileSubstituter;
        readonly IExtractPackage extractPackage;

        public DeployAzureServiceFabricAppCommand(
            ILog log,
            IScriptEngine scriptEngine,
            ICertificateStore certificateStore,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            ISubstituteInFiles substituteInFiles,
            IFileSubstituter fileSubstituter,
            IExtractPackage extractPackage
            )
        {
            Options.Add("package=", "Path to the NuGet package to install.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));

            this.log = log;
            this.scriptEngine = scriptEngine;
            this.certificateStore = certificateStore;
            this.variables = variables;
            this.commandLineRunner = commandLineRunner;
            this.substituteInFiles = substituteInFiles;
            this.fileSubstituter = fileSubstituter;
            this.extractPackage = extractPackage;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            if (!ServiceFabricHelper.IsServiceFabricSdkKeyInRegistry())
                throw new CommandException("Could not find the Azure Service Fabric SDK on this server. This SDK is required before running Service Fabric commands.");

            Guard.NotNullOrWhiteSpace(pathToPackage,
                "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(pathToPackage))
                throw new CommandException("Could not find package file: " + pathToPackage);

            Log.Info("Deploying package:    " + pathToPackage);

            var fileSystem = new WindowsPhysicalFileSystem();
            var embeddedResources = new AssemblyEmbeddedResources();
            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var jsonReplacer = new JsonConfigurationVariableReplacer();
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
            var transformFileLocator = new TransformFileLocator(fileSystem);
var packagedScriptService = new PackagedScriptService(log, fileSystem, scriptEngine, commandLineRunner);
var configuredScriptService = new ConfiguredScriptService(fileSystem, scriptEngine, commandLineRunner);

            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage)),

                // PreDeploy stage
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.PreDeploy),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.PreDeploy),

                // Standard variable and transform replacements
                new DelegateInstallConvention(d => substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(d)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsService(fileSystem, configurationTransformer, transformFileLocator)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesService(fileSystem, replacer)),
                new JsonConfigurationVariablesConvention(new JsonConfigurationVariablesService(jsonReplacer, fileSystem)),

                // Deploy stage
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.Deploy),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.Deploy),

                // Variable replacement
                new SubstituteVariablesInAzureServiceFabricPackageConvention(fileSystem, fileSubstituter),

                // Main Service Fabric deployment script execution
                new EnsureCertificateInstalledInStoreConvention(certificateStore, SpecialVariables.Action.ServiceFabric.ClientCertVariable, SpecialVariables.Action.ServiceFabric.CertificateStoreLocation, SpecialVariables.Action.ServiceFabric.CertificateStoreName),
                new DeployAzureServiceFabricAppConvention(log, fileSystem, embeddedResources, scriptEngine, commandLineRunner),

                // PostDeploy stage
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
