using System;
using System.Linq;
using Calamari.Shared;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;

namespace Calamari.Deployment.Journal
{
    
    public interface IDeploymentJournalWriter
    {
        void AddJournalEntry(IExecutionContext deployment, bool wasSuccessful, string packageFile = null);
    }
    
    public class DeploymentJournalWriter : IDeploymentJournalWriter
    {
        private readonly ICalamariFileSystem fileSystem;

        public DeploymentJournalWriter(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }
        
        /// <summary>
        /// Conditionally Write To Journal if there were packages that may need to be cleaned up during retention
        /// </summary>
        /// <param name="deployment"></param>
        /// <param name="wasSuccessful">Was the command successful. Ususally ExitCode == 0</param>
        /// <param name="packageFile">Since package references can still be passed by the command line this needs to be provided here.
        /// Can remove once we obtain all references through variables</param>
        public void AddJournalEntry(IExecutionContext deployment, bool wasSuccessful, string packageFile = null)
        {
            var semaphore = SemaphoreFactory.Get();
            var journal = new DeploymentJournal(fileSystem, semaphore, deployment.Variables);
            
            var hasPackages = !String.IsNullOrWhiteSpace(packageFile) ||
                              deployment.Variables.GetIndexes(SpecialVariables.Packages.PackageCollection).Any();
            
            var canWrite = deployment.Variables.Get(SpecialVariables.Tentacle.Agent.JournalPath) != null;
            var skipJournal = deployment.Variables.GetFlag(SpecialVariables.Action.SkipJournal, false);
            
            if(canWrite && hasPackages && !skipJournal)
                journal.AddJournalEntry(new JournalEntry(deployment, wasSuccessful));
        }
    }
}