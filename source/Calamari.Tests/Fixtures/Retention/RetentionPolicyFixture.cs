using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Deployment.Journal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Deployment.Retention;
using Calamari.Integration.Time;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Retention
{
    [TestFixture]
    public class RetentionPolicyFixture
    {
        RetentionPolicy retentionPolicy;
        ICalamariFileSystem fileSystem;
        IDeploymentJournal deploymentJournal;
        IClock clock;
        DateTimeOffset now;

        const string policySet1 = "policySet1";
        JournalEntry sevenDayOldSuccessfulDeployment;
        JournalEntry sevenDayOldUnsuccessfulDeployment;
        JournalEntry sixDayOldSuccessfulDeployment;
        JournalEntry fiveDayOldUnsuccessfulDeployment;
        JournalEntry fourDayOldSuccessfulDeployment;
        JournalEntry threeDayOldSuccessfulDeployment;
        JournalEntry twoDayOldSuccessfulDeployment;
        JournalEntry oneDayOldUnsuccessfulDeployment;
        JournalEntry fourDayOldSameLocationDeployment;
        JournalEntry fourDayOldMultiPackageDeployment;
        JournalEntry twoDayOldMultiPackageDeployment;
        JournalEntry twoDayOldDeploymentWithPackageThatWasNotAcquired;
        JournalEntry fiveDayOldDeploymentWithPackageThatWasNotAcquired;

        const string policySet2 = "policySet2";
        const string policySet3 = "policySet3";
        const string policySet4 = "policySet4";
        JournalEntry fiveDayOldNonMatchingDeployment;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            deploymentJournal = Substitute.For<IDeploymentJournal>();
            clock = Substitute.For<IClock>();
            retentionPolicy = new RetentionPolicy(fileSystem, deploymentJournal, clock);

            now = new DateTimeOffset(new DateTime(2015, 01, 15), new TimeSpan(0, 0, 0));
            clock.GetUtcTime().Returns(now);

            // Deployed 7 days prior to 'now'
            sevenDayOldSuccessfulDeployment = new JournalEntry("sevenDayOldSuccessful", "blah", "blah", "blah", policySet1,
                 now.AddDays(-7).LocalDateTime, "C:\\Applications\\Acme.0.0.7", null, true,
                 new DeployedPackage("blah", "blah", "C:\\packages\\Acme.0.0.7.nupkg"));
            sevenDayOldUnsuccessfulDeployment = new JournalEntry("sevenDayOldUnsuccessful", "blah", "blah", "blah", policySet1,
                now.AddDays(-7).LocalDateTime, "C:\\Applications\\Acme.0.0.7", null, false,
                new DeployedPackage("blah", "blah", "C:\\packages\\Acme.0.0.7.nupkg"));

            // Deployed 6 days prior to 'now'
            sixDayOldSuccessfulDeployment = new JournalEntry("sixDayOldSuccessful", "blah", "blah", "blah", policySet1,
                now.AddDays(-6).LocalDateTime, "C:\\Applications\\Acme.0.0.8", null, true,
                new DeployedPackage("blah", "blah", "C:\\packages\\Acme.0.0.8.nupkg"));

            // Deployed 5 days prior to 'now'
            fiveDayOldUnsuccessfulDeployment = new JournalEntry("fiveDayOldUnsuccessful", "blah", "blah", "blah", policySet1,
                now.AddDays(-5).LocalDateTime, "C:\\Applications\\Acme.0.0.9", null, false,
                new DeployedPackage("blah", "blah", "C:\\packages\\Acme.0.0.9.nupkg"));

            // Deployed 4 days prior to 'now'
            fourDayOldSuccessfulDeployment = new JournalEntry("fourDayOldSuccessful", "blah", "blah", "blah", policySet1,
                now.AddDays(-4).LocalDateTime, "C:\\Applications\\Acme.1.0.0", null, true,
                new DeployedPackage("blah", "blah", "C:\\packages\\Acme.1.0.0.nupkg"));

            // Deployed 4 days prior to 'now' but to the same location as the latest successful deployment
            fourDayOldSameLocationDeployment = new JournalEntry("fourDayOldSameLocation", "blah", "blah", "blah", policySet1,
                now.AddDays(-4).LocalDateTime, "C:\\Applications\\Acme.1.2.0", null, true,
                new DeployedPackage("blah", "blah", "C:\\packages\\Acme.1.2.0.nupkg"));

            // Deployed 3 days prior to 'now'
            threeDayOldSuccessfulDeployment = new JournalEntry("threeDayOldSuccessful", "blah", "blah", "blah", policySet1,
                now.AddDays(-3).LocalDateTime, "C:\\Applications\\Acme.1.1.0", null, true,
                new DeployedPackage("blah", "blah", "C:\\packages\\Acme.1.1.0.nupkg"));

            // Deployed 2 days prior to 'now'
            twoDayOldSuccessfulDeployment = new JournalEntry("twoDayOldSuccessful", "blah", "blah", "blah", policySet1,
                now.AddDays(-2).LocalDateTime, "C:\\Applications\\Acme.1.2.0", null, true,
                new DeployedPackage("blah", "blah", "C:\\packages\\Acme.1.2.0.nupkg"));

            // Deployed (unsuccessfully) 1 day prior to 'now'
            oneDayOldUnsuccessfulDeployment = new JournalEntry("oneDayOldUnsuccessful", "blah", "blah", "blah", policySet1,
                now.AddDays(-1).LocalDateTime, "C:\\Applications\\Acme.1.3.0", null, false,
                new DeployedPackage("blah", "blah", "C:\\packages\\Acme.1.3.0.nupkg"));

            // Deployed 5 days prior to 'now', but has a different policy-set
            fiveDayOldNonMatchingDeployment = new JournalEntry("fiveDayOld", "blah", "blah", "blah", policySet2,
                now.AddDays(-5).LocalDateTime, "C:\\Applications\\Beta.1.0.0", null, true,
                new DeployedPackage("blah", "blah", "C:\\packages\\Beta.1.0.0.nupkg"));

            // Step with multiple packages, deployed 4 days prior, and referencing the same source file as the latest successful 
            // deployment from a different step
            fourDayOldMultiPackageDeployment = new JournalEntry("fourDayOldMultiPackage", "blah", "blah", "blah", policySet3,
                now.AddDays(-4).LocalDateTime, null, null, true,
                new[]
                {
                    new DeployedPackage("blah", "blah", "C:\\packages\\Acme.1.2.0.nupkg"),
                    new DeployedPackage("foo", "blah", "C:\\packages\\Foo.0.0.9.nupkg")
                });

            // Step with multiple packages, deployed 2 days prior 
            twoDayOldMultiPackageDeployment = new JournalEntry("twoDayOldMultiPackage", "blah", "blah", "blah", policySet3,
                now.AddDays(-2).LocalDateTime, null, null, true,
                new[]
                {
                    new DeployedPackage("blah", "blah", "C:\\packages\\Acme.1.2.0.nupkg"),
                    new DeployedPackage("foo", "blah", "C:\\packages\\Foo.1.0.0.nupkg")
                });

            // We may reference packages which are not acquired (e.g. docker containers from script steps).
            // These will not have a `DeployedFrom` path.
            twoDayOldDeploymentWithPackageThatWasNotAcquired = new JournalEntry("twoDayOldNotAcquired",
                "blah", "blah", "blah", policySet4,
                now.AddDays(-2).LocalDateTime, null, null, true,
                new[]
                {
                    new DeployedPackage("blah", "blah", null)
                });

            fiveDayOldDeploymentWithPackageThatWasNotAcquired = new JournalEntry("fiveDayOldNotAcquired",
                "blah", "blah", "blah", policySet4,
                now.AddDays(-5).LocalDateTime, null, null, true,
                new[]
                {
                    new DeployedPackage("blah", "blah", null)
                });


            var journalEntries = new List<JournalEntry>
            {
                sevenDayOldUnsuccessfulDeployment,
                sevenDayOldSuccessfulDeployment,
                sixDayOldSuccessfulDeployment,
                fiveDayOldUnsuccessfulDeployment,
                fiveDayOldNonMatchingDeployment,
                fourDayOldSuccessfulDeployment,
                threeDayOldSuccessfulDeployment,
                twoDayOldSuccessfulDeployment,
                oneDayOldUnsuccessfulDeployment,
                fourDayOldMultiPackageDeployment,
                twoDayOldMultiPackageDeployment,
                fiveDayOldDeploymentWithPackageThatWasNotAcquired
            };

            deploymentJournal.GetAllJournalEntries().Returns(journalEntries);

            foreach (var journalEntry in journalEntries)
            {
                if (!string.IsNullOrEmpty(journalEntry.ExtractedTo))
                    fileSystem.DirectoryExists(journalEntry.ExtractedTo).Returns(true);

                foreach (var deployedPackage in journalEntry.Packages)
                    fileSystem.FileExists(deployedPackage.DeployedFrom).Returns(true);
            }

            Environment.SetEnvironmentVariable("TentacleHome", @"Q:\TentacleHome");

        }

        [Test]
        public void ShouldNotDeleteDirectoryWhereRetainedDeployedToSame()
        {
            var journalEntries = new List<JournalEntry>
            {
                fourDayOldSuccessfulDeployment,
                fourDayOldSameLocationDeployment,
                twoDayOldSuccessfulDeployment,
            };
            deploymentJournal.GetAllJournalEntries().Returns(journalEntries);
            fileSystem.FileExists(fourDayOldSameLocationDeployment.Package.DeployedFrom).Returns(true);
            fileSystem.DirectoryExists(fourDayOldSameLocationDeployment.ExtractedTo).Returns(true);

            const int days = 3;
            retentionPolicy.ApplyRetentionPolicy(policySet1, days, null);

            // Ensure the directories are the same
            Assert.AreEqual(twoDayOldSuccessfulDeployment.ExtractedTo, fourDayOldSameLocationDeployment.ExtractedTo);

            // The old directory was not removed...
            fileSystem.DidNotReceive().DeleteDirectory(Arg.Is<string>(s => s.Equals(fourDayOldSameLocationDeployment.ExtractedTo)));

            // ...despite being removed from the journal
            deploymentJournal.Received().RemoveJournalEntries(Arg.Is<IEnumerable<string>>(ids => ids.Contains(fourDayOldSameLocationDeployment.Id)));

            // and unique directory still removed
            fileSystem.Received().DeleteDirectory(Arg.Is<string>(s => s.Equals(fourDayOldSuccessfulDeployment.ExtractedTo)));

        }

        [Test]
        public void ShouldKeepDeploymentsForSpecifiedDays()
        {
            deploymentJournal.GetAllJournalEntries().Returns(new List<JournalEntry>
            {
                fourDayOldSuccessfulDeployment,
                threeDayOldSuccessfulDeployment,
                twoDayOldSuccessfulDeployment,
                oneDayOldUnsuccessfulDeployment,
                fourDayOldMultiPackageDeployment,
                twoDayOldMultiPackageDeployment,
                fiveDayOldDeploymentWithPackageThatWasNotAcquired
            });

            retentionPolicy.ApplyRetentionPolicy(policySet1, 3, null);

            // The older artifacts should have been removed
            fileSystem.Received().DeleteDirectory(fourDayOldSuccessfulDeployment.ExtractedTo);
            fileSystem.Received().DeleteFile(fourDayOldSuccessfulDeployment.Package.DeployedFrom, Arg.Any<FailureOptions>());

            // The newer artifacts, and those from the non-matching policy-set, should have been kept
            // In other words, nothing but the matching deployment should have been removed
            fileSystem.DidNotReceive().DeleteDirectory(Arg.Is<string>(s => !s.Equals(fourDayOldSuccessfulDeployment.ExtractedTo)));
            fileSystem.DidNotReceive().DeleteFile(Arg.Is<string>(s => !s.Equals(fourDayOldSuccessfulDeployment.Package.DeployedFrom)), Arg.Any<FailureOptions>());

            // The older entry should have been removed from the journal
            deploymentJournal.Received().RemoveJournalEntries(Arg.Is<IEnumerable<string>>(ids => ids.Count() == 1 && ids.Contains(fourDayOldSuccessfulDeployment.Id)));
        }

        [Test]
        public void ShouldKeepSpecifiedNumberOfDeployments()
        {
            var deploymentsExpectedToKeep = new List<JournalEntry>
            {
                twoDayOldSuccessfulDeployment,
                sixDayOldSuccessfulDeployment,
                fiveDayOldNonMatchingDeployment // Non-matching
            };
            var deploymentsExpectedToRemove = new List<JournalEntry>
            {
                fiveDayOldUnsuccessfulDeployment,
                sevenDayOldUnsuccessfulDeployment,
                sevenDayOldSuccessfulDeployment
            };
            deploymentJournal.GetAllJournalEntries().Returns(deploymentsExpectedToKeep.Concat(deploymentsExpectedToRemove).ToList());

            // Keep 1 (+ the current) deployment
            // This will not count the unsuccessful deployment
            retentionPolicy.ApplyRetentionPolicy(policySet1, null, 1);

            foreach (var deployment in deploymentsExpectedToRemove)
            {
                fileSystem.Received().DeleteDirectory(deployment.ExtractedTo);
                fileSystem.Received().DeleteFile(deployment.Package.DeployedFrom, Arg.Any<FailureOptions>());
            }

            // The newer artifacts, and those from the non-matching policy-set, should have been kept
            // In other words, nothing but the matching deployment should have been removed

            foreach (var deployment in deploymentsExpectedToKeep)
            {
                fileSystem.DidNotReceive().DeleteDirectory(deployment.ExtractedTo);
                fileSystem.DidNotReceive().DeleteFile(deployment.Package.DeployedFrom, Arg.Any<FailureOptions>());
            }

            // The older entry should have been removed from the journal
            deploymentJournal.Received().RemoveJournalEntries(Arg.Is<IEnumerable<string>>(ids => ids.All(id => deploymentsExpectedToRemove.Any(d => d.Id == id))));
        }

        [Test]
        public void ShouldNotDeleteDirectoryWhenItIsPreserved()
        {
            var journalEntries = new List<JournalEntry>
            {
                twoDayOldSuccessfulDeployment,
                sevenDayOldUnsuccessfulDeployment,
                sevenDayOldSuccessfulDeployment
            };
            deploymentJournal.GetAllJournalEntries().Returns(journalEntries);

            // Keep 1 (+ the current) deployment
            // This will not count the unsuccessful deployment
            retentionPolicy.ApplyRetentionPolicy(policySet1, null, 1);

            fileSystem.DidNotReceive().DeleteDirectory(sevenDayOldUnsuccessfulDeployment.ExtractedTo);
            fileSystem.DidNotReceive().DeleteFile(sevenDayOldUnsuccessfulDeployment.Package.DeployedFrom, Arg.Any<FailureOptions>());

            deploymentJournal.Received().RemoveJournalEntries(Arg.Is<IEnumerable<string>>(ids => ids.Contains(sevenDayOldUnsuccessfulDeployment.Id)));
        }

        [Test]
        public void ShouldNotDeleteAnythingWhenSpecifiedNumberOfSuccessfulDeploymentsNotMet()
        {
            var journalEntries = new List<JournalEntry>
            {
                twoDayOldSuccessfulDeployment,
                oneDayOldUnsuccessfulDeployment,
            };
            deploymentJournal.GetAllJournalEntries().Returns(journalEntries);

            // Keep 1 (+ the current) deployment
            // This will not count the unsuccessful deployment
            retentionPolicy.ApplyRetentionPolicy(policySet1, null, 1);

            fileSystem.DidNotReceive().DeleteDirectory(Arg.Any<string>());
            fileSystem.DidNotReceive().DeleteFile(Arg.Any<string>(), Arg.Any<FailureOptions>());

            deploymentJournal.Received().RemoveJournalEntries(Arg.Is<IEnumerable<string>>(ids => !ids.Any()));
        }

        [Test]
        public void ShouldApplyRetentionToMultiplePackages()
        {
            const int days = 3;
            retentionPolicy.ApplyRetentionPolicy(policySet3, days, null);

            // One deployment will be cleaned, and one will be kept.
            // The one being cleaned contains two packages, one which has a DeployedFrom value which matches the kept deployment, and so will not be deleted.
            // The other package (foo) should be deleted.
            var fooPackage = fourDayOldMultiPackageDeployment.Packages.Single(p => p.PackageId == "foo");
            fileSystem.Received().DeleteFile(fooPackage.DeployedFrom, Arg.Any<FailureOptions>());
            fileSystem.DidNotReceive().DeleteFile(Arg.Is<string>(s => !s.Equals(fooPackage.DeployedFrom)), Arg.Any<FailureOptions>());

            // The older entry should have been removed from the journal
            deploymentJournal.Received().RemoveJournalEntries(Arg.Is<IEnumerable<string>>(ids => ids.Count() == 1 && ids.Contains(fourDayOldMultiPackageDeployment.Id)));
        }

        [Test]
        public void ShouldNotExplodeIfPackageWasNotAcquired()
        {
            const int days = 3;
            retentionPolicy.ApplyRetentionPolicy(policySet4, days, null);

            // The entry should have been removed from the journal
            deploymentJournal.Received().RemoveJournalEntries(Arg.Is<IEnumerable<string>>(ids => ids.Count() == 1 && ids.Contains(fiveDayOldDeploymentWithPackageThatWasNotAcquired.Id)));
        }
    }
}