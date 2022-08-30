using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class FirstInFirstOutJournalEntrySort : ISortJournalEntries
    {
        public IEnumerable<JournalEntry> Sort(IEnumerable<JournalEntry> journalEntries)
        {
            return journalEntries.OrderBy(entry => entry, new FirstInFirstOutJournalEntryComparer());
        }
    }
}