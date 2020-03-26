using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Features;
using Calamari.Deployment.Features.Java;
using Calamari.Deployment.Journal;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Packages.Java;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Calamari.Variables;

namespace Calamari.Commands.Java
{
    [Command("deploy-java-archive", Description = "Deploys a Java archive (.jar, .war, .ear)")]
    public class DeployJavaArchiveCommand : Command
    {
        string archiveFile;
        private readonly IScriptEngine scriptEngine;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly ILog log;

        public DeployJavaArchiveCommand(IScriptEngine scriptEngine, IVariables variables, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner, ILog log)
        {
            Options.Add("archive=", "Path to the Java archive to deploy.", v => archiveFile = Path.GetFullPath(v));

            this.scriptEngine = scriptEngine;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.log = log;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(archiveFile, "No archive file was specified. Please pass --archive YourPackage.jar");
            JavaRuntime.VerifyExists();
            
            if (!File.Exists(archiveFile))
                throw new CommandException("Could not find archive file: " + archiveFile);

            Log.Info("Deploying:    " + archiveFile);

            var semaphore = SemaphoreFactory.Get();
            var journal = new DeploymentJournal(fileSystem, semaphore, variables);
            var substituter = new FileSubstituter(fileSystem);

            var jsonReplacer = new JsonConfigurationVariableReplacer();
            var jarTools = new JarTool(commandLineRunner, log,  variables);
            var packageExtractor = new JavaPackageExtractor(jarTools);
            var embeddedResources = new AssemblyEmbeddedResources();
            var javaRunner = new JavaRunner(commandLineRunner, variables);
            
            
            var featureClasses = new List<IFeature>
            {
                new TomcatFeature(javaRunner),
                new WildflyFeature(javaRunner)
            };

            var deployExploded = variables.GetFlag(SpecialVariables.Action.Java.DeployExploded);

            var conventions = new List<IConvention>
            {
                new AlreadyInstalledConvention(journal),
                // If we are deploying the package exploded then extract directly to the application directory.
                // Else, if we are going to re-pack, then we extract initially to a temporary directory 
                deployExploded
                    ? (IInstallConvention)new ExtractPackageToApplicationDirectoryConvention(packageExtractor, fileSystem) 
                    : new ExtractPackageToStagingDirectoryConvention(packageExtractor, fileSystem),
                new FeatureConvention(DeploymentStages.BeforePreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new FeatureConvention(DeploymentStages.AfterPreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new SubstituteInFilesConvention(fileSystem, substituter),
                new JsonConfigurationVariablesConvention(jsonReplacer, fileSystem),
                new RePackArchiveConvention(fileSystem, jarTools),                
                new CopyPackageToCustomInstallationDirectoryConvention(fileSystem),
                new FeatureConvention(DeploymentStages.BeforeDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new PackagedScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.Deploy, fileSystem, scriptEngine, commandLineRunner),
                new FeatureConvention(DeploymentStages.AfterDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new FeatureConvention(DeploymentStages.BeforePostDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new PackagedScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptEngine, commandLineRunner),
                new FeatureConvention(DeploymentStages.AfterPostDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new RollbackScriptConvention(DeploymentStages.DeployFailed, fileSystem, scriptEngine, commandLineRunner),
                new FeatureRollbackConvention(DeploymentStages.DeployFailed, fileSystem, scriptEngine, commandLineRunner, embeddedResources)
            };

            var deployment = new RunningDeployment(archiveFile, variables);
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