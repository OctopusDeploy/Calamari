using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public interface IJournalRepository
    {
        bool TryGetJournalEntry(PackageIdentity package, out JournalEntry entry);
        JournalEntry GetJournalEntry(PackageIdentity packageID);
        void AddJournalEntry(JournalEntry entry);
        void Commit();
    }
}