using System;
using System.Collections.Generic;

namespace Calamari.Common.Features.Deployment.Journal
{
    public interface IDeploymentJournal
    {
        List<JournalEntry> GetAllJournalEntries();
        void RemoveJournalEntries(IEnumerable<string> ids);
        JournalEntry GetLatestInstallation(string retentionPolicySubset);
        JournalEntry GetLatestInstallation(string retentionPolicySubset, string packageId, string packageVersion);
        JournalEntry GetLatestSuccessfulInstallation(string retentionPolicySubset);
        JournalEntry GetLatestSuccessfulInstallation(string retentionPolicySubset, string packageId, string packageVersion);
    }
}