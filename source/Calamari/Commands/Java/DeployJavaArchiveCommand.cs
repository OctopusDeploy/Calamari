using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Deployment.Journal;
using Calamari.Common.Features.EmbeddedResources;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.Decorators;
using Calamari.Common.Features.Packages.Java;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Features;
using Calamari.Deployment.Features.Java;

namespace Calamari.Commands.Java
{
    [Command("deploy-java-archive", Description = "Deploys a Java archive (.jar, .war, .ear)")]
    public class DeployJavaArchiveCommand : Command
    {
        PathToPackage archiveFile;
        readonly ILog log;
        readonly IScriptEngine scriptEngine;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly ISubstituteInFiles substituteInFiles;
        readonly IExtractPackage extractPackage;
        readonly IStructuredConfigVariablesService structuredConfigVariablesService;
        readonly IDeploymentJournalWriter deploymentJournalWriter;

        public DeployJavaArchiveCommand(
            ILog log,
            IScriptEngine scriptEngine,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage,
            IStructuredConfigVariablesService structuredConfigVariablesService,
            IDeploymentJournalWriter deploymentJournalWriter)
        {
            Options.Add("archive=", "Path to the Java archive to deploy.", v => archiveFile = new PathToPackage(Path.GetFullPath(v)));

            this.log = log;
            this.scriptEngine = scriptEngine;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.substituteInFiles = substituteInFiles;
            this.extractPackage = extractPackage;
            this.structuredConfigVariablesService = structuredConfigVariablesService;
            this.deploymentJournalWriter = deploymentJournalWriter;
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
            var jarTools = new JarTool(commandLineRunner, log, variables);
            var packageExtractor = new JarPackageExtractor(jarTools).WithExtractionLimits(log, variables);
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
                new AlreadyInstalledConvention(log, journal),
                // If we are deploying the package exploded then extract directly to the application directory.
                // Else, if we are going to re-pack, then we extract initially to a temporary directory
                deployExploded
                    ? (IInstallConvention)new DelegateInstallConvention(d => extractPackage.ExtractToApplicationDirectory(archiveFile, packageExtractor))
                    : new DelegateInstallConvention(d => extractPackage.ExtractToStagingDirectory(archiveFile, packageExtractor)),
                new FeatureConvention(DeploymentStages.BeforePreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new ConfiguredScriptConvention(new PreDeployConfiguredScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new PackagedScriptConvention(new PreDeployPackagedScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new FeatureConvention(DeploymentStages.AfterPreDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new SubstituteInFilesConvention(new SubstituteInFilesBehaviour(substituteInFiles)),
                new StructuredConfigurationVariablesConvention(new StructuredConfigurationVariablesBehaviour(structuredConfigVariablesService)),
                new RePackArchiveConvention(log, fileSystem, jarTools),
                new CopyPackageToCustomInstallationDirectoryConvention(fileSystem),
                new FeatureConvention(DeploymentStages.BeforeDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new PackagedScriptConvention(new DeployPackagedScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new ConfiguredScriptConvention(new DeployConfiguredScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new FeatureConvention(DeploymentStages.AfterDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new FeatureConvention(DeploymentStages.BeforePostDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new PackagedScriptConvention(new PostDeployPackagedScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new ConfiguredScriptConvention(new PostDeployConfiguredScriptBehaviour(log, fileSystem, scriptEngine, commandLineRunner)),
                new FeatureConvention(DeploymentStages.AfterPostDeploy, featureClasses, fileSystem, scriptEngine, commandLineRunner, embeddedResources),
                new RollbackScriptConvention(log, DeploymentStages.DeployFailed, fileSystem, scriptEngine, commandLineRunner),
                new FeatureRollbackConvention(DeploymentStages.DeployFailed, fileSystem, scriptEngine, commandLineRunner, embeddedResources)
            };

            var deployment = new RunningDeployment(archiveFile, variables);
            var conventionRunner = new ConventionProcessor(deployment, conventions, log);

            try
            {
                conventionRunner.RunConventions();
                deploymentJournalWriter.AddJournalEntry(deployment, true);
            }
            catch (Exception)
            {
                deploymentJournalWriter.AddJournalEntry(deployment, false);
                throw;
            }

            return 0;
        }
    }
}