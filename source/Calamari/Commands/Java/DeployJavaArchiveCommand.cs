using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Conventions.CopyConventions;
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
using Calamari.Java.Deployment.Conventions;

namespace Calamari.Commands.Java
{
    [Command("deploy-java-archive", Description = "Deploys a Java archive (.jar, .war, .ear)")]
    public class DeployJavaArchiveCommand : Command
    {
        string variablesFile;
        string archiveFile;
        string sensitiveVariablesFile;
        string sensitiveVariablesPassword;

        public DeployJavaArchiveCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("archive=", "Path to the Java archive to deploy.", v => archiveFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(archiveFile, "No archive file was specified. Please pass --archive YourPackage.jar");

            if (!File.Exists(archiveFile))
                throw new CommandException("Could not find archive file: " + archiveFile);

            Log.Info("Deploying:    " + archiveFile);
            
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);

            var semaphore = SemaphoreFactory.Get();
            var journal = new DeploymentJournal(fileSystem, semaphore, variables);
            var scriptEngine = new CombinedScriptEngine();
            var substituter = new FileSubstituter(fileSystem);
            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var jsonReplacer = new JsonConfigurationVariableReplacer();
            var packageExtractor = new JavaPackageExtractor(commandLineRunner, fileSystem);
            var embeddedResources = new AssemblyEmbeddedResources();

            var featureClasses = new List<IFeature>
            {
                new TomcatFeature(commandLineRunner),
                new WildflyFeature(commandLineRunner)
            };

            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new ContributePreviousInstallationConvention(journal),
                new ContributePreviousSuccessfulInstallationConvention(journal),
                new LogVariablesConvention(),
                new AlreadyInstalledConvention(journal),
                new ExtractPackageToStagingDirectoryConvention(packageExtractor, fileSystem),
                new FeatureConvention(DeploymentStages.BeforePreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptEngine, commandLineRunner),
                new FeatureConvention(DeploymentStages.AfterPreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new SubstituteInFilesConvention(fileSystem, substituter),
                new JsonConfigurationVariablesConvention(jsonReplacer, fileSystem),
                new RePackArchiveConvention(fileSystem, packageExtractor, commandLineRunner),
                new CopyRepackedPackageToCustomInstallationDirectoryConvention(fileSystem),
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