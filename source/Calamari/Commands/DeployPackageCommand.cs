using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Features;
using Calamari.Deployment.Journal;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Iis;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Nginx;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;

namespace Calamari.Commands
{
    [Command("deploy-package", Description = "Extracts and installs a deployment package")]
    public class DeployPackageCommand : Command
    {
        private string packageFile;
        private readonly CombinedScriptEngine scriptEngine;
        readonly IVariables variables;

        public DeployPackageCommand(CombinedScriptEngine scriptEngine, IVariables variables)
        {
            Options.Add("package=", "Path to the deployment package to install.", v => packageFile = Path.GetFullPath(v));

            this.scriptEngine = scriptEngine;
            this.variables = variables;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(packageFile, "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);    

            Log.Info("Deploying package:    " + packageFile);
            
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var featureClasses = new List<IFeature>();

            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var generator = new JsonConfigurationVariableReplacer();
            var substituter = new FileSubstituter(fileSystem);
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
            var transformFileLocator = new TransformFileLocator(fileSystem);
            var embeddedResources = new AssemblyEmbeddedResources();
#if IIS_SUPPORT
            var iis = new InternetInformationServer();
            featureClasses.AddRange(new IFeature[] { new IisWebSiteBeforeDeployFeature(), new IisWebSiteAfterPostDeployFeature() });
#endif
            if (!CalamariEnvironment.IsRunningOnWindows)
            {
                featureClasses.Add(new NginxFeature(NginxServer.AutoDetect()));
            }

            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var semaphore = SemaphoreFactory.Get();
            var journal = new DeploymentJournal(fileSystem, semaphore, variables);

            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new ContributePreviousInstallationConvention(journal),
                new ContributePreviousSuccessfulInstallationConvention(journal),
                new LogVariablesConvention(),
                new AlreadyInstalledConvention(journal),
                new ExtractPackageToApplicationDirectoryConvention(new GenericPackageExtractorFactory().createStandardGenericPackageExtractor(), fileSystem),
                new FeatureConvention(DeploymentStages.BeforePreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new FeatureConvention(DeploymentStages.AfterPreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new SubstituteInFilesConvention(fileSystem, substituter),
                new ConfigurationTransformsConvention(fileSystem, configurationTransformer, transformFileLocator),
                new ConfigurationVariablesConvention(fileSystem, replacer),
                new JsonConfigurationVariablesConvention(generator, fileSystem),
                new CopyPackageToCustomInstallationDirectoryConvention(fileSystem),
                new FeatureConvention(DeploymentStages.BeforeDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new PackagedScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new FeatureConvention(DeploymentStages.AfterDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
#if IIS_SUPPORT
                new LegacyIisWebSiteConvention(fileSystem, iis),
#endif
                new FeatureConvention(DeploymentStages.BeforePostDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new PackagedScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner),
                new FeatureConvention(DeploymentStages.AfterPostDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new RollbackScriptConvention(DeploymentStages.DeployFailed, fileSystem, scriptEngine, commandLineRunner),
                new FeatureRollbackConvention(DeploymentStages.DeployFailed, fileSystem, scriptEngine, commandLineRunner, embeddedResources)
            };

            var deployment = new RunningDeployment(packageFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions);

            try
            {
                conventionRunner.RunConventions();
                if (!deployment.SkipJournal) 
                    journal.AddJournalEntry(new JournalEntry(deployment, true));
            }
            catch (Exception)
            {
                if (!deployment.SkipJournal) 
                    journal.AddJournalEntry(new JournalEntry(deployment, false));
                throw;
            }

            return 0;
        }
    }
}
