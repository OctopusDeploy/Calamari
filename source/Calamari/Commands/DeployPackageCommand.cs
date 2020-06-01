﻿using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Common.Features.Scripting;
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
using Calamari.Variables;

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
        PathToPackage pathToPackage;

        public DeployPackageCommand(
            ILog log,
            IScriptEngine scriptEngine,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage
        )
        {
            Options.Add("package=", "Path to the deployment package to install.", v => pathToPackage = new PathToPackage(Path.GetFullPath(v)));

            this.log = log;
            this.scriptEngine = scriptEngine;
            this.variables = variables;
            this.fileSystem = fileSystem;
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

            var featureClasses = new List<IFeature>();

            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var generator = new JsonConfigurationVariableReplacer();
            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
            var transformFileLocator = new TransformFileLocator(fileSystem);
            var embeddedResources = new AssemblyEmbeddedResources();
#if IIS_SUPPORT
            var iis = new InternetInformationServer();
            featureClasses.AddRange(new IFeature[] { new IisWebSiteBeforeDeployFeature(), new IisWebSiteAfterPostDeployFeature() });
#endif
            if (!CalamariEnvironment.IsRunningOnWindows)
            {
                featureClasses.Add(new NginxFeature(NginxServer.AutoDetect(), fileSystem));
            }

            var semaphore = SemaphoreFactory.Get();
            var journal = new DeploymentJournal(fileSystem, semaphore, variables);
            var packagedScriptService = new PackagedScriptService(log, fileSystem, scriptEngine, commandLineRunner);
            var configuredScriptService = new ConfiguredScriptService(fileSystem, scriptEngine, commandLineRunner);

            var conventions = new List<IConvention>
            {
                new AlreadyInstalledConvention(log, journal),
                new DelegateInstallConvention(d => extractPackage.ExtractToApplicationDirectory(pathToPackage)),
                new FeatureConvention(DeploymentStages.BeforePreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.PreDeploy),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.PreDeploy),
                new FeatureConvention(DeploymentStages.AfterPreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new DelegateInstallConvention(d => substituteInFiles.SubstituteBasedSettingsInSuppliedVariables(d)),
                new ConfigurationTransformsConvention(new ConfigurationTransformsService(fileSystem, configurationTransformer, transformFileLocator)),
                new ConfigurationVariablesConvention(new ConfigurationVariablesService(fileSystem, replacer)),
                new JsonConfigurationVariablesConvention(new JsonConfigurationVariablesService(generator, fileSystem)),
                new CopyPackageToCustomInstallationDirectoryConvention(fileSystem),
                new FeatureConvention(DeploymentStages.BeforeDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.Deploy),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.Deploy),
                new FeatureConvention(DeploymentStages.AfterDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
#if IIS_SUPPORT
                new LegacyIisWebSiteConvention(fileSystem, iis),
#endif
                new FeatureConvention(DeploymentStages.BeforePostDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new PackagedScriptConvention(packagedScriptService, DeploymentStages.PostDeploy),
                new ConfiguredScriptConvention(configuredScriptService, DeploymentStages.PostDeploy),
                new FeatureConvention(DeploymentStages.AfterPostDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new RollbackScriptConvention(log, DeploymentStages.DeployFailed, fileSystem, scriptEngine, commandLineRunner),
                new FeatureRollbackConvention(DeploymentStages.DeployFailed, fileSystem, scriptEngine, commandLineRunner, embeddedResources)
            };

            var deployment = new RunningDeployment(pathToPackage, variables);
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