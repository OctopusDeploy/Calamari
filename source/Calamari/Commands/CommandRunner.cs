using System;
using Calamari.Deployment.Journal;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;

namespace Calamari.Commands
{
    public class CommandRunner
    {
        private readonly DeploymentStrategyBuilder cb;
        private readonly ICalamariFileSystem fileSystem;
        private readonly IDeploymentJournalWriter deploymentJournalWriter;
        private readonly ILog log;

        public CommandRunner(DeploymentStrategyBuilder cb, ICalamariFileSystem fileSystem, IDeploymentJournalWriter deploymentJournalWriter, ILog log)
        {
            this.cb = cb;
            this.fileSystem = fileSystem;
            this.deploymentJournalWriter = deploymentJournalWriter;
            this.log = log;
        }

        public int Run(CalamariVariableDictionary variables, string packageFile)
        {
            var ctx = new CalamariExecutionContext()
            {
                Variables = variables,
                PackageFilePath = packageFile,
                Log = log
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
}