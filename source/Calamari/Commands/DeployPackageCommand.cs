using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Integration.AppSettingsJson;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Iis;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Octostache;

namespace Calamari.Commands
{
    [Command("deploy-package", Description = "Extracts and installs a deployment package")]
    public class DeployPackageCommand : Command
    {
        private string variablesFile;
        private string packageFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;

        public DeployPackageCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("package=", "Path to the deployment package to install.", v => packageFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            Guard.NotNullOrWhiteSpace(packageFile, "No package file was specified. Please pass --package YourPackage.nupkg");

            if (!File.Exists(packageFile))
                throw new CommandException("Could not find package file: " + packageFile);    

            Log.Info("Deploying package:    " + packageFile);
            
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);
            var scriptCapability = new CombinedScriptEngine();
            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
            var generator = new AppSettingsJsonGenerator();
            var substituter = new FileSubstituter(fileSystem);
            var configurationTransformer = new ConfigurationTransformer(variables.GetFlag(SpecialVariables.Package.IgnoreConfigTransformationErrors), variables.GetFlag(SpecialVariables.Package.SuppressConfigTransformationLogging));
            var embeddedResources = new CallingAssemblyEmbeddedResources();
            var iis = new InternetInformationServer();
            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var semaphore = new SystemSemaphore();
            var journal = new DeploymentJournal(fileSystem, semaphore, variables);

            var conventions = new List<IConvention>
            {
                new ContributeEnvironmentVariablesConvention(),
                new ContributePreviousInstallationConvention(journal),
                new LogVariablesConvention(),
                new AlreadyInstalledConvention(journal),
                new ExtractPackageToApplicationDirectoryConvention(new GenericPackageExtractor(), fileSystem, semaphore),
                new FeatureScriptConvention(DeploymentStages.BeforePreDeploy, fileSystem, embeddedResources, scriptCapability, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.PreDeploy, scriptCapability, fileSystem, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PreDeploy, fileSystem, scriptCapability, commandLineRunner),
                new FeatureScriptConvention(DeploymentStages.AfterPreDeploy, fileSystem, embeddedResources, scriptCapability, commandLineRunner),
                new SubstituteInFilesConvention(fileSystem, substituter),
                new ConfigurationTransformsConvention(fileSystem, configurationTransformer),
                new ConfigurationVariablesConvention(fileSystem, replacer),
                new AppSettingsJsonConvention(generator),
                new CopyPackageToCustomInstallationDirectoryConvention(fileSystem),
                new FeatureScriptConvention(DeploymentStages.BeforeDeploy, fileSystem, embeddedResources, scriptCapability, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.Deploy, fileSystem, scriptCapability, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.Deploy, scriptCapability, fileSystem, commandLineRunner),
                new FeatureScriptConvention(DeploymentStages.AfterDeploy, fileSystem, embeddedResources, scriptCapability, commandLineRunner),
                new LegacyIisWebSiteConvention(fileSystem, iis),
                new FeatureScriptConvention(DeploymentStages.BeforePostDeploy, fileSystem, embeddedResources, scriptCapability, commandLineRunner),
                new PackagedScriptConvention(DeploymentStages.PostDeploy, fileSystem, scriptCapability, commandLineRunner),
                new ConfiguredScriptConvention(DeploymentStages.PostDeploy, scriptCapability, fileSystem, commandLineRunner),
                new FeatureScriptConvention(DeploymentStages.AfterPostDeploy, fileSystem, embeddedResources, scriptCapability, commandLineRunner),
                new RollbackScriptConvention(DeploymentStages.DeployFailed, fileSystem, scriptCapability, commandLineRunner)
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
