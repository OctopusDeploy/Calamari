using System.IO;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class VariableJsonJournalPathProvider : IJsonJournalPathProvider
    {
        const string DefaultJournalName = "PackageRetentionJournal.json";
        readonly IVariables variables;

        public VariableJsonJournalPathProvider(IVariables variables)
        {
            this.variables = variables;
        }

        public string GetJournalPath()
        {
            var packageRetentionJournalPath = variables.Get(KnownVariables.Calamari.PackageRetentionJournalPath);
            if (packageRetentionJournalPath != null)
                return packageRetentionJournalPath;

            var tentacleHome = variables.Get(TentacleVariables.Agent.TentacleHome) ?? string.Empty;
            return Path.Combine(tentacleHome, DefaultJournalName);
        }
    }
}