using System;
using System.Collections.Generic;
using System.Configuration;
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
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;

namespace Calamari.Commands
{

    public class CommandRunner
    {
        private readonly CommandBuilder cb;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IDeploymentJournalWriter deploymentJournalWriter;

        public CommandRunner(CommandBuilder cb, ICalamariFileSystem fileSystem, IDeploymentJournalWriter deploymentJournalWriter)
        {
            this.cb = cb;
            this.fileSystem = fileSystem;
            this.deploymentJournalWriter = deploymentJournalWriter;
        }

        public int Run(CalamariVariableDictionary variables, string packageFile)
        {
            var ctx = new CalamariExecutionContext()
            {
                Variables = variables,
                PackageFilePath = packageFile
            };
            
            var journal = cb.UsesDeploymentJournal ?
                new DeploymentJournal(fileSystem, SemaphoreFactory.Get(), ctx.Variables) :
                null;
            
            

            CalamariPhysicalFileSystem.FreeDiskSpaceOverrideInMegaBytes = ctx.Variables.GetInt32(SpecialVariables.FreeDiskSpaceOverrideInMegaBytes);
            CalamariPhysicalFileSystem.SkipFreeDiskSpaceCheck = ctx.Variables.GetFlag(SpecialVariables.SkipFreeDiskSpaceCheck);
            
            try
            {
                RunConventions(ctx, journal);
                deploymentJournalWriter.AddJournalEntry(ctx, true);
                //journal?.AddJournalEntry(new JournalEntry(ctx, true));
            }
            catch (Exception)
            {
                deploymentJournalWriter.AddJournalEntry(ctx, false);
                //journal?.AddJournalEntry(new JournalEntry(ctx, false));
                throw;
            }

            return 0;
        }
        
        void RunInstallConventions(IExecutionContext ctx, IDeploymentJournal journal)
        {
            foreach (var convention in cb.BuildConventionSteps(journal))
            {
                if (ctx.Variables.GetFlag(SpecialVariables.Action.SkipRemainingConventions))
                {
                    break;
                }
                
                convention(ctx);
            }
        }
        
        void RunRollbackConventions(IExecutionContext ctx)
        {
            foreach (var convention in cb.BuildRollbackScriptSteps())
            {
                convention(ctx);
            }
        }
        
        void RunRollbackCleanup(IExecutionContext ctx)
        {
            foreach (var convention in cb.BuildCleanupScriptSteps())
            {
                if (ctx.Variables.GetFlag(SpecialVariables.Action.SkipRemainingConventions))
                {
                    break;
                }
                
                convention(ctx);
            }
        }
        
        
        
        void RunConventions(IExecutionContext ctx, IDeploymentJournal journal)
        {
            try
            {
                RunInstallConventions(ctx, journal);
                RunRollbackCleanup(ctx);
            }
            catch (Exception ex)
            {
                if (ex is CommandException)
                {
                    Console.Error.WriteLine(ex.Message);
                }
                else
                {
                    Console.Error.WriteLine(ex);
                }
                Console.Error.WriteLine("Running rollback conventions...");

                ex = ex.GetBaseException();
                ctx.Variables.Set(SpecialVariables.LastError, ex.ToString());
                ctx.Variables.Set(SpecialVariables.LastErrorMessage, ex.Message);

                // Rollback conventions include tasks like DeployFailed.ps1
                RunRollbackConventions(ctx);

                // Run cleanup for rollback conventions, for example: delete DeployFailed.ps1 script
                RunRollbackCleanup(ctx);

                throw;
            }
        }
    }
    
    
    
    [Command("deploy-package", Description = "Extracts and installs a deployment package")]
    public class DeployPackageCommand : Command, Calamari.Shared.Commands.ICustomCommand
    {
        private readonly ICalamariFileSystem filesystem;
//        private string variablesFile;
//        private string packageFile;
//        private string sensitiveVariablesFile;
//        private string sensitiveVariablesPassword;
//        private CombinedScriptEngine scriptCapability;

        public DeployPackageCommand(ICalamariFileSystem filesystem)
        {
            this.filesystem = filesystem;
        }
        
        public void DeployPackageCommand2(CombinedScriptEngine scriptCapability)
        {
//            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
//            Options.Add("package=", "Path to the deployment package to install.", v => packageFile = Path.GetFullPath(v));
//            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
//            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);

            //this.scriptCapability = scriptCapability;
        }

      
   

        public ICommandBuilder Run(ICommandBuilder cb)
        {

            cb.UsesDeploymentJournal = true;

            
#if IIS_SUPPORT
            cb.Features.Add<IisWebSiteBeforeDeployFeature>();
            cb.Features.Add<IisWebSiteAfterPostDeployFeature>();
            var iis = new InternetInformationServer();
#endif
            
            cb.AddExtractPackageToApplicationDirectory()
                .RunPreScripts()
                .AddSubsituteInFiles()
                .AddConfigurationTransform()
                .AddConfigurationVariables()
                .AddJsonVariables()
                .AddConvention<CopyPackageToCustomInstallationDirectoryConvention>()
                .RunDeployScripts();
            
#if IIS_SUPPORT
            cb.AddConvention(new LegacyIisWebSiteConvention(filesystem, iis));
#endif

            cb.RunPostScripts();

            return cb;
//            var cr = new CommandRunner(cb, fileSystem);
//            cr.Run(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword, packageFile);
        }

        public override int Execute(string[] commandLineArguments)
        {
            throw new NotImplementedException();
        }
    }
}
