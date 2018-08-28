namespace Calamari.Deployment.Journal
{
    public interface IDeploymentJournalWriter
    {
        void AddJournalEntry(RunningDeployment deployment, bool wasSuccessful, string packageFile = null);
    }
}