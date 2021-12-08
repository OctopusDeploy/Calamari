using System;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Deployment.PackageRetention.VersionFormatDiscovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Tests.Fixtures.PackageRetention.Repository;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.PackageRetention
{
    [TestFixture]
    public class JournalVersionFormatDiscoveryFixture
    {
        [Test]
        public void WhenValidArgumentsProvided_ThenReturnCorrectFormatAndTrue()
        {
            var packageId = "package-1";
            var version = "1.0.0";
            var taskId = "Task-1";

            SetupRetention(out var variables, packageId, version, taskId);

            var discovery = new JournalVersionFormatDiscovery();
            var journal = new Journal(new InMemoryJournalRepositoryFactory(), variables, Substitute.For<IRetentionAlgorithm>(), Substitute.For<ILog>());

            journal.RegisterPackageUse(new PackageIdentity(packageId, version, VersionFormat.Docker), new ServerTaskId("Task-2")); //Different task, forces discovery using just using the packageId and version.
            journal.RegisterPackageUse(new PackageIdentity(packageId, "2.0.0", VersionFormat.Maven), new ServerTaskId("Task-3")); //Different task/version, should be ignored.

            var success = discovery.TryDiscoverVersionFormat(journal, variables, Array.Empty<string>(), out var format);

            Assert.That(success);
            Assert.That(format == VersionFormat.Docker);
        }

        [Test]
        public void TestArgumentsAndResultsForFindingWithServerTaskId()
        {
            var packageId = "package-1";
            var version = "1.0.0";
            var taskId = "Task-2";

            SetupRetention(out var variables, packageId, version, taskId);
            var discovery = new JournalVersionFormatDiscovery();
            var journal = new Journal(new InMemoryJournalRepositoryFactory(), variables, Substitute.For<IRetentionAlgorithm>(), Substitute.For<ILog>());

            journal.RegisterPackageUse(new PackageIdentity(packageId, "2.0.0", VersionFormat.Maven), new ServerTaskId("Task-1")); //Different task, different version - should be ignored.
            journal.RegisterPackageUse(new PackageIdentity(packageId, "2.0.0", VersionFormat.Docker), new ServerTaskId("Task-2")); //Same task, different version - ensures it determines format using the taskId and packageId only.

            var success = discovery.TryDiscoverVersionFormat(journal, variables, Array.Empty<string>(),out var format);

            Assert.That(success);
            Assert.That(format == VersionFormat.Docker);
        }

        void SetupRetention(out CalamariVariables variables, string packageId, string version, string taskId)
        {
            variables = new CalamariVariables
            {
                { KnownVariables.Calamari.PackageRetentionJournalPath, "journal.json" },
                { KnownVariables.Calamari.EnablePackageRetention, Boolean.TrueString },
                { PackageVariables.PackageId, packageId },
                { PackageVariables.PackageVersion, version },
                { KnownVariables.ServerTask.Id, taskId }
            };
        }
    }
}