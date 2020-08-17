using System;
using Calamari.Common.Commands;

namespace Calamari.Common.Plumbing.Deployment.Journal
{
    public interface IDeploymentJournalWriter
    {
        void AddJournalEntry(RunningDeployment deployment, bool wasSuccessful, string? packageFile = null);
    }
}