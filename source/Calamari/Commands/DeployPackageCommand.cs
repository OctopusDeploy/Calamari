using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Deployment.Journal;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Features;
using Calamari.Integration.Certificates;
using Calamari.Integration.FullFramework;
using Calamari.Integration.Iis;
using Calamari.Integration.Nginx;

namespace Calamari.Commands
{
    [Command("deploy-package", Description = "Extracts and installs a deployment package")]
    public class DeployPackageCommand : Command
    {
        readonly ILog log;
        readonly IScriptEngine scriptEngine;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IExtractPackage extractPackage;
        readonly IStructuredConfigVariablesService structuredConfigVariablesService;
        readonly IDeploymentJournalWriter deploymentJournalWriter;
        readonly IWindowsX509CertificateStore windowsX509CertificateStore;
        PathToPackage pathToPackage;

        public DeployPackageCommand(
            ILog log,
            IScriptEngine scriptEngine,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage,
            IStructuredConfigVariablesService structuredConfigVariablesService,
            IDeploymentJournalWriter deploymentJournalWriter,
            IWindowsX509CertificateStore windowsX509CertificateStore)
        {
            Options.Add("package=", "Path to the deployment package to install.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));

            this.log = log;
            this.scriptEngine = scriptEngine;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.substituteInFiles = substituteInFiles;
            this.extractPackage = extractPackage;
            this.structuredConfigVariablesService = structuredConfigVariablesService;
            this.deploymentJournalWriter = deploymentJournalWriter;
            this.windowsX509CertificateStore = windowsX509CertificateStore;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(pathToPackage, "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(pathToPackage))
                throw new CommandException("Could not find package file: " + pathToPackage);

            Log.Info("Deploying package:    " + pathToPackage);

            var featureClasses = new List<IFeature>();

            var replacer = new ConfigurationVariablesReplacer(variables, log);
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables, log);
            var transformFileLocator = new TransformFileLocator(fileSystem, log);
            var embeddedResources = new AssemblyEmbeddedResources();
#if IIS_SUPPORT
            var iis = !OctopusFeatureToggles.FullFrameworkTasksExternalProcess.IsEnabled(variables) ? 
                new InternetInformationServer(): (IInternetInformationServer)new LegacyInternetInformationServer(new LegacyFrameworkInvoker());
            featureClasses.AddRange(new IFeature[] { new IisWebSiteBeforeDeployFeature(windowsX509CertificateStore), new IisWebSiteAfterPostDeployFeature(windowsX509CertificateStore) });
#endif
            if (!CalamariEnvironment.IsRunningOnWindows)
            {
                featureClasses.Add(new NginxFeature(NginxServer.AutoDetect(), fileSystem));
            }

            var semaphore = SemaphoreFactory.Get();
            var journal = new DeploymentJournal(fileSystem, semaphore, variables);

            var conventions = new List<IConvention>
            {
                new AlreadyInstalledConvention(log, journal),
                new DelegateInstallConvention(d => extractPackage.ExtractToApplicationDirectory(pathToPackage)),
                new FeatureConvention(DeploymentStages.BeforePreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new ConfiguredScriptConvention(new PreDeployConfiguredScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new PackagedScriptConvention(new PreDeployPackagedScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new FeatureConvention(DeploymentStages.AfterPreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new SubstituteInFilesConvention(new SubstituteInFilesBehaviour(substituteInFiles)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsBehaviour(fileSystem, variables, configurationTransformer, transformFileLocator, log)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesBehaviour(fileSystem, variables, replacer, log)),
                new StructuredConfigurationVariablesConvention(new StructuredConfigurationVariablesBehaviour(structuredConfigVariablesService)),
                new CopyPackageToCustomInstallationDirectoryConvention(fileSystem),
                new FeatureConvention(DeploymentStages.BeforeDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new PackagedScriptConvention(new DeployPackagedScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new ConfiguredScriptConvention(new DeployConfiguredScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new FeatureConvention(DeploymentStages.AfterDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
#if IIS_SUPPORT
                new LegacyIisWebSiteConvention(fileSystem, iis),
#endif
                new FeatureConvention(DeploymentStages.BeforePostDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new PackagedScriptConvention(new PostDeployPackagedScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new ConfiguredScriptConvention(new PostDeployConfiguredScriptBehaviour( log, fileSystem, scriptEngine, commandLineRunner)),
                new FeatureConvention(DeploymentStages.AfterPostDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new RollbackScriptConvention(log, DeploymentStages.DeployFailed, fileSystem, scriptEngine, commandLineRunner),
                new FeatureRollbackConvention(DeploymentStages.DeployFailed, fileSystem, scriptEngine, commandLineRunner, embeddedResources)
            };

            var deployment = new RunningDeployment(pathToPackage, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions, log);

            try
            {
                conventionRunner.RunConventions();
                deploymentJournalWriter.AddJournalEntry(deployment, true, pathToPackage);
            }
            catch (Exception)
            {
                deploymentJournalWriter.AddJournalEntry(deployment, false, pathToPackage);
                throw;
            }

            return 0;
        }
    }
}