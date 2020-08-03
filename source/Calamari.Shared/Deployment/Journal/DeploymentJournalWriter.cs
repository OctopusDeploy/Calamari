﻿using System;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment.Journal;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.Journal
{
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
        public void AddJournalEntry(RunningDeployment deployment, bool wasSuccessful, string? packageFile = null)
        {
            var semaphore = SemaphoreFactory.Get();
            var journal = new DeploymentJournal(fileSystem, semaphore, deployment.Variables);
            
            var hasPackages = !string.IsNullOrWhiteSpace(packageFile) ||
                              deployment.Variables.GetIndexes(PackageVariables.PackageCollection).Any();
            
            var canWrite = deployment.Variables.Get(TentacleVariables.Agent.JournalPath) != null;
            
            if(canWrite && hasPackages && !deployment.SkipJournal)
                journal.AddJournalEntry(new JournalEntry(deployment, wasSuccessful));
        }
    }
}