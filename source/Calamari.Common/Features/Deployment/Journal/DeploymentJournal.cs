using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Deployment.Journal
{
    public class DeploymentJournal : IDeploymentJournal
    {
        const string SemaphoreName = "Octopus.Calamari.DeploymentJournal";
        readonly ICalamariFileSystem fileSystem;
        readonly ISemaphoreFactory semaphore;
        readonly IVariables variables;

        public DeploymentJournal(ICalamariFileSystem fileSystem, ISemaphoreFactory semaphore, IVariables variables)
        {
            this.fileSystem = fileSystem;
            this.semaphore = semaphore;
            this.variables = variables;
        }

        string? JournalPath => variables.Get(TentacleVariables.Agent.JournalPath);

        internal void AddJournalEntry(JournalEntry entry)
        {
            using (semaphore.Acquire(SemaphoreName, "Another process is using the deployment journal"))
            {
                var xElement = entry.ToXmlElement();
                Log.VerboseFormat("Adding journal entry:\n{0}", xElement.ToString());
                Write(Read().Concat(new[] { xElement }));
            }
        }

        public List<JournalEntry> GetAllJournalEntries()
        {
            using (semaphore.Acquire(SemaphoreName, "Another process is using the deployment journal"))
            {
                return Read().Select(element => new JournalEntry(element)).ToList();
            }
        }

        public void RemoveJournalEntries(IEnumerable<string> ids)
        {
            using (semaphore.Acquire(SemaphoreName, "Another process is using the deployment journal"))
            {
                var elements = Read();

                Write(elements.Where(x =>
                {
                    var id = x.Attribute("Id");
                    return id == null || !ids.Contains(id.Value);
                }));
            }
        }

        public JournalEntry GetLatestInstallation(string retentionPolicySubset)
        {
            return GetLatestInstallation(retentionPolicySubset, null, null);
        }

        public JournalEntry GetLatestInstallation(string retentionPolicySubset, string? packageId, string? packageVersion)
        {
            return GetAllJournalEntries()
                .Where(e =>
                    string.Equals(retentionPolicySubset, e.RetentionPolicySet, StringComparison.OrdinalIgnoreCase) &&
                    (packageId == null && packageVersion == null ||
                        e.Packages.Any(deployedPackage =>
                            (packageId == null || string.Equals(packageId, deployedPackage.PackageId, StringComparison.OrdinalIgnoreCase)) &&
                            (packageVersion == null || string.Equals(packageVersion, deployedPackage.PackageVersion, StringComparison.OrdinalIgnoreCase)))))
                .OrderByDescending(o => o.InstalledOn)
                .FirstOrDefault();
        }

        public JournalEntry GetLatestSuccessfulInstallation(string retentionPolicySubset)
        {
            return GetLatestSuccessfulInstallation(retentionPolicySubset, null, null);
        }

        public JournalEntry GetLatestSuccessfulInstallation(string retentionPolicySubset,
            string? packageId,
            string? packageVersion)
        {
            return GetAllJournalEntries()
                .Where(e =>
                    string.Equals(retentionPolicySubset, e.RetentionPolicySet, StringComparison.OrdinalIgnoreCase) &&
                    (packageId == null && packageVersion == null ||
                        e.Packages.Any(deployedPackage =>
                            (packageId == null || string.Equals(packageId, deployedPackage.PackageId, StringComparison.OrdinalIgnoreCase)) &&
                            (packageVersion == null || string.Equals(packageVersion, deployedPackage.PackageVersion, StringComparison.OrdinalIgnoreCase)))) &&
                    e.WasSuccessful)
                .OrderByDescending(o => o.InstalledOn)
                .FirstOrDefault();
        }

        IEnumerable<XElement> Read()
        {
            if (!fileSystem.FileExists(JournalPath))
                yield break;

            using (var file = fileSystem.OpenFile(JournalPath, FileAccess.Read))
            {
                var document = XDocument.Load(file);

                foreach (var element in document.Element("Deployments").Elements())
                    yield return element;
            }
        }

        void Write(IEnumerable<XElement> elements)
        {
            if (string.IsNullOrWhiteSpace(JournalPath))
                throw new InvalidOperationException("JournalPath has not been set");

            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(JournalPath));

            var tempPath = JournalPath + ".temp-" + Guid.NewGuid() + ".xml";

            var root = new XElement("Deployments");
            var document = new XDocument(root);
            foreach (var element in elements)
                root.Add(element);

            using (var stream = fileSystem.OpenFile(tempPath, FileMode.Create, FileAccess.Write))
            {
                document.Save(stream);
            }

            fileSystem.OverwriteAndDelete(JournalPath, tempPath);
        }
    }
}