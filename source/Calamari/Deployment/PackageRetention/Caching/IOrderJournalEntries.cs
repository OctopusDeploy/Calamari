using System.Collections.Generic;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public interface IOrderJournalEntries
    {
        IEnumerable<JournalEntry> Order(IEnumerable<JournalEntry> journalEntries);
    }
}