﻿using System.Collections.Generic;
using System.IO;
using Calamari.Azure.ServiceFabric.Deployment.Conventions;
using Calamari.Azure.ServiceFabric.Util;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.EmbeddedResources;
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
using Calamari.Integration.Certificates;

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
            var replacer = new ConfigurationVariablesReplacer(variables, log);
            var allFileFormatReplacers = FileFormatVariableReplacers.BuildAllReplacers(fileSystem, log);
            var structuredConfigFileService = new StructuredConfigVariablesService(allFileFormatReplacers, fileSystem, log);
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables, log);
            var transformFileLocator = new TransformFileLocator(fileSystem, log);

            var conventions = new List<IConvention>
            {
                new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(pathToPackage)),

                // PreDeploy stage
                new ConfiguredScriptConvention(new ConfiguredScriptBehaviour(DeploymentStages.PreDeploy, log, fileSystem, scriptEngine, commandLineRunner)),
                new PackagedScriptConvention(new PackagedScriptBehaviour(log, DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner)),

                // Standard variable and transform replacements
                new DelegateInstallConvention(d => substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(d)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(fileSystem, configurationTransformer, transformFileLocator, log)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesBehaviour(fileSystem, replacer, log)),
                new StructuredConfigurationVariablesConvention(new StructuredConfigurationVariablesBehaviour(structuredConfigFileService)),

                // Deploy stage
                new PackagedScriptConvention(new PackagedScriptBehaviour(log, DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner)),
                new ConfiguredScriptConvention(new ConfiguredScriptBehaviour(DeploymentStages.Deploy, log, fileSystem, scriptEngine, commandLineRunner)),

                // Variable replacement
                new SubstituteVariablesInAzureServiceFabricPackageConvention(fileSystem, fileSubstituter),

                // Main Service Fabric deployment script execution
                new EnsureCertificateInstalledInStoreConvention(certificateStore, SpecialVariables.Action.ServiceFabric.ClientCertVariable, SpecialVariables.Action.ServiceFabric.CertificateStoreLocation, SpecialVariables.Action.ServiceFabric.CertificateStoreName),
                new DeployAzureServiceFabricAppConvention(log, fileSystem, embeddedResources, scriptEngine, commandLineRunner),

                // PostDeploy stage
                new PackagedScriptConvention(new PackagedScriptBehaviour(log, DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner)),
                new ConfiguredScriptConvention(new ConfiguredScriptBehaviour(DeploymentStages.PostDeploy, log, fileSystem, scriptEngine, commandLineRunner))
            };

            var deployment = new RunningDeployment(pathToPackage, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions, log);
            conventionRunner.RunConventions();

            return 0;
        }
    }
}
