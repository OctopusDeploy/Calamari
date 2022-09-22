using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Tests.Fixtures.PackageRetention.Repository
{
    public class StaticJsonJournalPathProvider : IJsonJournalPathProvider
    {
        readonly string path;

        public StaticJsonJournalPathProvider(string path)
        {
            this.path = path;
        }

        public string GetJournalPath() => path;
    }
}