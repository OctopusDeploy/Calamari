using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;

namespace Calamari.Deployment
{
    public class PackageDeploymentFeatureRunner
    {
        private readonly IPackageDeploymentFeature feature;

        public PackageDeploymentFeatureRunner(IPackageDeploymentFeature feature)
        {
            this.feature = feature;
        }

        public void Install(IVariableDictionary variables)
        {
            var packageFile = variables.Get(SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath);


            Guard.NotNullOrWhiteSpace(packageFile,
                "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);

            Log.Info("Deploying package:    " + packageFile);

            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            fileSystem.FreeDiskSpaceOverrideInMegaBytes =
                variables.GetInt32(SpecialVariables.FreeDiskSpaceOverrideInMegaBytes);
            fileSystem.SkipFreeDiskSpaceCheck = variables.GetFlag(SpecialVariables.SkipFreeDiskSpaceCheck);

            var scriptCapability = new CombinedScriptEngine();
            var replacer =
                new ConfigurationVariablesReplacer(
                    variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var generator = new JsonConfigurationVariableReplacer();
            var substituter = new FileSubstituter(fileSystem);
            var configurationTransformer =
                new ConfigurationTransformer(
                    variables.GetFlag(SpecialVariables.Package.IgnoreConfigTransformationErrors),
                    variables.GetFlag(SpecialVariables.Package.SuppressConfigTransformationLogging));
            var transformFileLocator = new TransformFileLocator(fileSystem);
            var embeddedResources = new AssemblyEmbeddedResources();
//#if IIS_SUPPORT
//            var iis = new InternetInformationServer();
//#endif
            var commandLineRunner =
                new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(),
                    new ServiceMessageCommandOutput(variables)));
            var semaphore = SemaphoreFactory.Get();
            var journal = new DeploymentJournal(fileSystem, semaphore, variables);

            var conventions = new List<IConvention>
            {
                new ContributePreviousInstallationConvention(journal),
                new LogVariablesConvention(),
                new AlreadyInstalledConvention(journal),
                new ExtractPackageToApplicationDirectoryConvention(new GenericPackageExtractor(), fileSystem, semaphore),

                new FeatureScriptConvention(DeploymentStages.BeforePreDeploy, fileSystem, scriptCapability,
                    commandLineRunner, embeddedResources),
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptCapability,
                    commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptCapability, commandLineRunner),
                new FeatureScriptConvention(DeploymentStages.AfterPreDeploy, fileSystem, scriptCapability,
                    commandLineRunner, embeddedResources),

                new SubstituteInFilesConvention(fileSystem, substituter),
                new ConfigurationTransformsConvention(fileSystem, configurationTransformer, transformFileLocator),
                new ConfigurationVariablesConvention(fileSystem, replacer),
                new JsonConfigurationVariablesConvention(generator, fileSystem),
                new CopyPackageToCustomInstallationDirectoryConvention(fileSystem),

                new FeatureScriptConvention(DeploymentStages.BeforeDeploy, fileSystem, scriptCapability,
                    commandLineRunner, embeddedResources),
                new PackagedScriptConvention(DeploymentStages.Deploy, fileSystem, scriptCapability, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.Deploy, fileSystem, scriptCapability, commandLineRunner),
                new FeatureScriptConvention(DeploymentStages.AfterDeploy, fileSystem, scriptCapability,
                    commandLineRunner, embeddedResources),
                new FeatureInstallConvention(feature.AfterDeploy),

                new FeatureScriptConvention(DeploymentStages.BeforePostDeploy, fileSystem, scriptCapability,
                    commandLineRunner, embeddedResources),
                new PackagedScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptCapability,
                    commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptCapability,
                    commandLineRunner),
                new FeatureScriptConvention(DeploymentStages.AfterPostDeploy, fileSystem, scriptCapability,
                    commandLineRunner, embeddedResources),
                new RollbackScriptConvention(DeploymentStages.DeployFailed, fileSystem, scriptCapability,
                    commandLineRunner)

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

        }




        class FeatureInstallConvention : IInstallConvention
        {
            private readonly Action<IVariableDictionary> invoker;

            public FeatureInstallConvention(Action<IVariableDictionary> invoker)
            {
                this.invoker = invoker;
            }

            public void Install(RunningDeployment deployment)
            {
                invoker.Invoke(deployment.Variables);
            }
        }
    }
}