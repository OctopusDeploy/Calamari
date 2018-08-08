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
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Calamari.Integration.Substitutions;
using Calamari.Shared;

namespace Calamari.Commands
{
    [Command("deploy-package", Description = "Extracts and installs a deployment package")]
    public class DeployPackageCommand : Command
    {
        private string variablesFile;
        private string packageFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private CombinedScriptEngine scriptCapability;

        public DeployPackageCommand(CombinedScriptEngine scriptCapability)
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("package=", "Path to the deployment package to install.", v => packageFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);

            this.scriptCapability = scriptCapability;
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

            fileSystem.FreeDiskSpaceOverrideInMegaBytes = variables.GetInt32(SpecialVariables.FreeDiskSpaceOverrideInMegaBytes);
            fileSystem.SkipFreeDiskSpaceCheck = variables.GetFlag(SpecialVariables.SkipFreeDiskSpaceCheck);

//            var featureClasses = new List<IFeature>();
//
//            var replacer = new ConfigurationVariablesReplacer(variables.GetFlag(SpecialVariables.Package.IgnoreVariableReplacementErrors));
//            var generator = new JsonConfigurationVariableReplacer();
//            var substituter = new FileSubstituter(fileSystem);
//            var configurationTransformer = ConfigurationTransformer.FromVariables(variables);
//            var transformFileLocator = new TransformFileLocator(fileSystem);
//            var embeddedResources = new AssemblyEmbeddedResources();
//
//            var commandLineRunner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var semaphore = SemaphoreFactory.Get();
            var journal = new DeploymentJournal(fileSystem, semaphore, variables);


            var cb = new CommandBuilder(null);

            cb.UsesDeploymentJournal = true;
            
#if IIS_SUPPORT
            cb.Features.Add<IisWebSiteBeforeDeployFeature>();
            cb.Features.Add<IisWebSiteAfterPostDeployFeature>();
            var iis = new InternetInformationServer();
#endif
           
            
            
            
            cb.AddContributeEnvironmentVariables()
                .AddConvention(new ContributePreviousInstallationConvention(journal))
                .AddConvention(new ContributePreviousSuccessfulInstallationConvention(journal))
                .AddLogVariables()
                .AddConvention(new AlreadyInstalledConvention(journal))
                .AddExtractPackageToStagingDirectory()
                .RunPreScripts()
                .AddSubsituteInFiles()
                .AddConfigurationTransform()
                .AddConfigurationVariables()
                .AddJsonVariables()
                .AddConvention<CopyPackageToCustomInstallationDirectoryConvention>()
                .RunDeployScripts();
            
#if IIS_SUPPORT
                cb.AddConvention(new LegacyIisWebSiteConvention(fileSystem, iis))
#endif

            cb.RunPostScripts();

            var ctx = new CalamariExecutionContext()
            {
                Variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword),
                PackageFilePath = packageFile
            };
            
            
            try
            {
                foreach (var v in cb.ConventionSteps)
                {
                    v.Invoke(ctx);
                }
            }
            catch (Exception ex)
            {
                    
            }

         /*
          *public class CommandExecution
    {
        private readonly Container container;

        public CommandExecution(Container container)
        {
            this.container = container;
        }


        public void Run()
        {
            ICustomCommand blah = null;
            var blder = new CommandBuilder(container);

            
            
            var x = new CalamariExecutionContext();
            blah.Run(blder);


            foreach (var v in blder.ConventionSteps)
            {
                try
                {
                    v.Invoke(x);
                }
                catch (Exception ex)
                {
                    
                }
            }
        }

    }
          * 
          */
            

//            var deployment = new RunningDeployment(packageFile, variables);
//            var conventionRunner = new ConventionProcessor(deployment, conventions);
//
//            try
//            {
//                conventionRunner.RunConventions();
//                if (!deployment.SkipJournal) 
//                    journal.AddJournalEntry(new JournalEntry(deployment, true));
//            }
//            catch (Exception)
//            {
//                if (!deployment.SkipJournal) 
//                    journal.AddJournalEntry(new JournalEntry(deployment, false));
//                throw;
//            }

            return 0;
        }
    }
}
