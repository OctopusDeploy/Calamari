using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Iis;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using IPackageExtractor = Calamari.Extensibility.Features.IPackageExtractor;

namespace Calamari.Deployment
{

    public class BlahConvention : IInstallConvention
    {
        private readonly Action<IVariableDictionary> invoker;

        public BlahConvention(Action<IVariableDictionary>  invoker)
        {
            this.invoker = invoker;
        }
        public void Install(RunningDeployment deployment)
        {
            invoker.Invoke(deployment.Variables);
        }
    }

    public class PackageDeploymentFeatureRunner
    {
        private readonly IPackageDeploymentFeature feature;
        private readonly ICalamariFileSystem fileSystem;
        private readonly ISemaphoreFactory semaphore;

        public PackageDeploymentFeatureRunner(IPackageDeploymentFeature feature, ICalamariFileSystem fileSystem, ISemaphoreFactory semaphore)
        {
            this.feature = feature;
            this.fileSystem = fileSystem;
            this.semaphore = semaphore;
        }

        public void Install(IVariableDictionary variables)
        {
            var packageFile = variables.Get(SpecialVariables.Tentacle.CurrentDeployment.PackageFilePath);          


            Guard.NotNullOrWhiteSpace(packageFile, "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);    

            Log.Info("Deploying package:    " + packageFile);
            
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            fileSystem.FreeDiskSpaceOverrideInMegaBytes = variables.GetInt32(SpecialVariables.FreeDiskSpaceOverrideInMegaBytes);
            fileSystem.SkipFreeDiskSpaceCheck = variables.GetFlag(SpecialVariables.SkipFreeDiskSpaceCheck);

            var scriptCapability = new CombinedScriptEngine();
            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var generator = new JsonConfigurationVariableReplacer();
            var substituter = new FileSubstituter(fileSystem);
            var configurationTransformer = new ConfigurationTransformer(variables.GetFlag(SpecialVariables.Package.IgnoreConfigTransformationErrors), variables.GetFlag(SpecialVariables.Package.SuppressConfigTransformationLogging));
            var transformFileLocator = new TransformFileLocator(fileSystem);
            var embeddedResources = new AssemblyEmbeddedResources();
//#if IIS_SUPPORT
//            var iis = new InternetInformationServer();
//#endif
            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var semaphore = SemaphoreFactory.Get();
            var journal = new DeploymentJournal(fileSystem, semaphore, variables);

            var conventions = new List<IConvention>
            {
                new ContributePreviousInstallationConvention(journal),
                new LogVariablesConvention(),
                new AlreadyInstalledConvention(journal),
                new ExtractPackageToApplicationDirectoryConvention(new GenericPackageExtractor(), fileSystem, semaphore),
                
                new FeatureScriptConvention(DeploymentStages.BeforePreDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),
                    new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptCapability, commandLineRunner),
                    new PackagedScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptCapability, commandLineRunner),
                new FeatureScriptConvention(DeploymentStages.AfterPreDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),
                
                new SubstituteInFilesConvention(fileSystem, substituter),
                new ConfigurationTransformsConvention(fileSystem, configurationTransformer, transformFileLocator),
                new ConfigurationVariablesConvention(fileSystem, replacer),
                new JsonConfigurationVariablesConvention(generator, fileSystem),
                new CopyPackageToCustomInstallationDirectoryConvention(fileSystem),

                new FeatureScriptConvention(DeploymentStages.BeforeDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),
                    new PackagedScriptConvention(DeploymentStages.Deploy, fileSystem, scriptCapability, commandLineRunner),
                    new ConfiguredScriptConvention(DeploymentStages.Deploy, fileSystem, scriptCapability, commandLineRunner),
                new FeatureScriptConvention(DeploymentStages.AfterDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),
                new BlahConvention(feature.AfterDeploy),

                new FeatureScriptConvention(DeploymentStages.BeforePostDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),                
                    new PackagedScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptCapability, commandLineRunner),
                    new ConfiguredScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptCapability, commandLineRunner),                
                new FeatureScriptConvention(DeploymentStages.AfterPostDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),
                //new RollbackScriptConvention(DeploymentStages.DeployFailed, fileSystem, scriptCapability, commandLineRunner)
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
    }

  
    public class IISDeployment : IPackageDeploymentFeature
    {
        private readonly ICalamariFileSystem fileSystem;

        public IISDeployment(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void AfterDeploy(IVariableDictionary variables)
        {
            
#if IIS_SUPPORT
            new LegacyIisWebSiteConvention(fileSystem, new InternetInformationServer()).Install(null);
#endif
        }
    }





    [Feature("DeployPackage", "I Deploy Packages")]
    public class DeployPackageFeature : IFeature
    {
/*
 new ContributeEnvironmentVariablesConvention(),
                new ContributePreviousInstallationConvention(journal),
                new LogVariablesConvention(),
                new AlreadyInstalledConvention(journal),
                new ExtractPackageToApplicationDirectoryConvention(new GenericPackageExtractor(), fileSystem, semaphore),
                
BeforePreDeploy


            new FeatureScriptConvention(DeploymentStages.BeforePreDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),
                
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptCapability, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptCapability, commandLineRunner),
                
                new FeatureScriptConvention(DeploymentStages.AfterPreDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),
                new SubstituteInFilesConvention(fileSystem, substituter),
                new ConfigurationTransformsConvention(fileSystem, configurationTransformer, transformFileLocator),
                new ConfigurationVariablesConvention(fileSystem, replacer),
                new JsonConfigurationVariablesConvention(generator, fileSystem),
                new CopyPackageToCustomInstallationDirectoryConvention(fileSystem),
                
                new FeatureScriptConvention(DeploymentStages.BeforeDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),
                
                new PackagedScriptConvention(DeploymentStages.Deploy, fileSystem, scriptCapability, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.Deploy, fileSystem, scriptCapability, commandLineRunner),
                
                new FeatureScriptConvention(DeploymentStages.AfterDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),
//#if IIS_SUPPORT
                new LegacyIisWebSiteConvention(fileSystem, iis),
//#endif
                new FeatureScriptConvention(DeploymentStages.BeforePostDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),
                
                new PackagedScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptCapability, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptCapability, commandLineRunner),
                new FeatureScriptConvention(DeploymentStages.AfterPostDeploy, fileSystem, scriptCapability, commandLineRunner, embeddedResources),
                

                new RollbackScriptConvention(DeploymentStages.DeployFailed, fileSystem, scriptCapability, commandLineRunner)
     
*/
        private readonly IPackageExtractor extractor;
        private readonly IScriptExecution executor;
        private readonly IFileSubstitution substitutor;

        public DeployPackageFeature(IPackageExtractor extractor, IScriptExecution executor, IFileSubstitution substitutor)
        {
            this.extractor = extractor;
            this.executor = executor;
            this.substitutor = substitutor;
        }

        public void Install(IVariableDictionary variables)
        {
            var script = variables.Get(Shared.SpecialVariables.Action.Script.Path);
            var parameters = variables.Get(Shared.SpecialVariables.Action.Script.Parameters);
            var package = variables.Get(Shared.SpecialVariables.Action.Script.PackagePath);

            if (!string.IsNullOrWhiteSpace(package))
            {
                extractor.Extract(package, PackageExtractionLocation.WorkingDirectory);
            }

            substitutor.PerformSubstitution(script);
            executor.Invoke(script, parameters);
        }
    }
}
