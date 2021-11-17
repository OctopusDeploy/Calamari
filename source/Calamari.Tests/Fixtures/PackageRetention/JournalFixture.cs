using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class JournalFixture
    {
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "Journal");

        [SetUp]
        public void SetUp()
        {
            if (!Directory.Exists(TentacleHome))
                Directory.CreateDirectory(TentacleHome);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(TentacleHome))
                Directory.Delete(TentacleHome, true);
        }

        [Test]
        public void WhenMultipleThreadsTryToWriteToTheJournalAtTheSameTime_ThenTheWritesAreHandledCorrectly()
        {
            //TODO: Do this using Autofac
            TypeDescriptor.AddAttributes(typeof(ServerTaskId), new TypeConverterAttribute(typeof(TinyTypeTypeConverter<ServerTaskId>)));

            // var journalPath = Path.Combine(TentacleHome, "PackageRetentionJournal.json");

            // var variables = Substitute.For<IVariables>();
            // variables.Get(KnownVariables.Calamari.PackageRetentionJournalPath).Returns(journalPath);

            var repositoryFactory = new JsonJournalRepositoryFactory(TestCalamariPhysicalFileSystem.GetPhysicalFileSystem(), SemaphoreFactory.Get());

            var tasks = new List<Task>();
            for (var i = 1; i <= 5; i++)
            {
                var journal = new Journal(repositoryFactory, new InMemoryLog());
                var package = new PackageIdentity($"Package-{i}", $"0.0.{i}");
                var serverTask = new ServerTaskId($"ServerTasks-{i}");
                tasks.Add(Task.Run(() => journal.RegisterPackageUse(package, serverTask)));
            }

            Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(30));

            var json = File.ReadAllText(@"C:\Octopus\PackageJournal.json");
            var journalEntries = JsonConvert.DeserializeObject<List<JournalEntry>>(json);
            journalEntries.Count.Should().Be(5);
        }
    }
}