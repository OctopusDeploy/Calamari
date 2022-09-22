using System.Collections.Generic;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public interface ISortJournalEntries
    {
        IEnumerable<JournalEntry> Sort(IEnumerable<JournalEntry> journalEntries);
    }
}