namespace Calamari.Deployment.PackageRetention.Repositories
{
    public interface IJournalRepositoryFactory
    {
        IJournalRepository CreateJournalRepository();
    }
}