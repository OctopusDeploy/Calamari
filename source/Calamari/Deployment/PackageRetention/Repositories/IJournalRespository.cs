namespace Calamari.Deployment.PackageRetention.Repositories
{
    public interface IJournalRepository
    {
        bool TryGetJournalEntry(PackageID packageID, out JournalEntry entry);
        JournalEntry GetJournalEntry(PackageID packageID);
        void AddJournalEntry(JournalEntry entry);
        void Commit();
    }
}