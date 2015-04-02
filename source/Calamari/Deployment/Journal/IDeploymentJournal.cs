using System.Collections.Generic;

namespace Calamari.Deployment.Journal
{
    public interface IDeploymentJournal
    {
        void AddJournalEntry(JournalEntry entry);
        IEnumerable<JournalEntry> GetAllJournalEntries();
        void RemoveJournalEntries(IEnumerable<string> ids);
    }
}