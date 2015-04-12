using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Deployment.Journal
{
    public class DeploymentJournal : IDeploymentJournal
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ISemaphore semaphore;
        readonly VariableDictionary variables;
        const string SemaphoreName = "Calamari:DeploymentJournal";

        public DeploymentJournal(ICalamariFileSystem fileSystem, ISemaphore semaphore, VariableDictionary variables)
        {
            this.fileSystem = fileSystem;
            this.semaphore = semaphore;
            this.variables = variables;
        }

        private string JournalPath
        {
            get { return variables.Get(SpecialVariables.Tentacle.Agent.JournalPath); }
        }

        public void AddJournalEntry(JournalEntry entry)
        {
            using (semaphore.Acquire(SemaphoreName, "Another process is updating the deployment journal"))
            {
                var xElement = entry.ToXmlElement();
                Log.VerboseFormat("Adding journal entry:\n{0}", xElement.ToString());
                Write(Read().Concat(new[] {xElement}));
            }
        }

        public IEnumerable<JournalEntry> GetAllJournalEntries()
        {
            using (semaphore.Acquire(SemaphoreName, "Another process is updating the deployment journal"))
            {
                return Read().Select(element => new JournalEntry(element));
            }
        }

        public void RemoveJournalEntries(IEnumerable<string> ids)
        {
            using (semaphore.Acquire(SemaphoreName, "Another process is updating the deployment journal"))
            {
                var elements = Read();

                Write(elements.Where(x =>
                {
                    var id = x.Attribute("Id");
                    return id == null || !ids.Contains(id.Value);
                }));
            }
        }

        private IEnumerable<XElement> Read()
        {
            if (!fileSystem.FileExists(JournalPath))
                yield break; 
                
            using (var file = fileSystem.OpenFile(JournalPath, FileAccess.Read))
            {
                var document = XDocument.Load(file);

                foreach (var element in document.Element("Deployments").Elements())
                {
                    yield return element;
                }
            }
        }

        private void Write(IEnumerable<XElement> elements)
        {
            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(JournalPath));

            var tempPath = JournalPath + ".temp-" + Guid.NewGuid() + ".xml";

            var root = new XElement("Deployments");
            var document = new XDocument(root);
            foreach (var element in elements)
                root.Add(element);

            using (var stream = fileSystem.OpenFile(tempPath, FileMode.Create, FileAccess.Write))
                document.Save(stream);

            fileSystem.OverwriteAndDelete(JournalPath, tempPath);
        }
    }
}