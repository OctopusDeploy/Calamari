using System;
using System.Linq;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Util;

namespace Calamari.Deployment.Journal
{
    [RegisterMe]
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
        /// <param name="wasSuccessful">Was the command successful. Usually ExitCode == 0</param>
        /// <param name="packageFile">Since package references can still be passed by the command line this needs to be provided here.
        /// Can remove once we obtain all references through variables</param>
        public void AddJournalEntry(RunningDeployment deployment, bool wasSuccessful, string packageFile = null)
        {
            var semaphore = SemaphoreFactory.Get();
            var journal = new DeploymentJournal(fileSystem, semaphore, deployment.Variables);
            
            var hasPackages = !String.IsNullOrWhiteSpace(packageFile) ||
                              deployment.Variables.GetIndexes(SpecialVariables.Packages.PackageCollection).Any();
            
            var canWrite = deployment.Variables.Get(SpecialVariables.Tentacle.Agent.JournalPath) != null;
            
            if(canWrite && hasPackages && !deployment.SkipJournal)
                journal.AddJournalEntry(new JournalEntry(deployment, wasSuccessful));
        }
    }
}