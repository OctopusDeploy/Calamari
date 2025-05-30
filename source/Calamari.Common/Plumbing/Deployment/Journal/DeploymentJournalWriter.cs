﻿using System;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment.Journal;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Plumbing.Deployment.Journal
{
    public class DeploymentJournalWriter : IDeploymentJournalWriter
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public DeploymentJournalWriter(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
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
            if (deployment.SkipJournal)
                return;
            var semaphore = new SystemSemaphoreManager();
            var journal = new DeploymentJournal(fileSystem, semaphore, deployment.Variables, log);

            var hasPackages = !string.IsNullOrWhiteSpace(packageFile) || deployment.Variables.GetIndexes(PackageVariables.PackageCollection).Any();

            if (!hasPackages)
                return;

            var canWrite = deployment.Variables.Get(TentacleVariables.Agent.JournalPath) != null;

            if (!canWrite)
                return;

            var journalEntry = new JournalEntry(deployment, wasSuccessful);
            if (string.IsNullOrEmpty(journalEntry.ExtractedTo))
                return;

            journal.AddJournalEntry(new JournalEntry(deployment, wasSuccessful));
        }
    }
}