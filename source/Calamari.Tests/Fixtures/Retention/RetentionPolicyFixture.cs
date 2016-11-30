using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Calamari.Deployment.Journal;
using Calamari.Deployment.Retention;
using Calamari.Extensibility.FileSystem;
using Calamari.Integration.Time;
using NSubstitute;
using NUnit.Framework;
using FailureOptions = Calamari.Extensibility.FileSystem.FailureOptions;
using System.Reflection;

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
        JournalEntry fourDayOldDeployment;
        JournalEntry threeDayOldDeployment;
        JournalEntry twoDayOldDeployment;
        JournalEntry oneDayOldUnsuccessfulDeployment;
        JournalEntry fourDayOldSameLocationDeployment;

        const string policySet2 = "policySet2";
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

            // Deployed 4 days prior to 'now'
            fourDayOldDeployment = new JournalEntry("fourDayOld", "blah", "blah", "blah", "blah", "blah", policySet1,
                now.AddDays(-4).LocalDateTime,
                "C:\\packages\\Acme.1.0.0.nupkg", "C:\\Applications\\Acme.1.0.0", null, true);

            // Deployed 4 days prior to 'now' but to the same location as the latest successful deployment
            fourDayOldSameLocationDeployment = new JournalEntry("twoDayOld", "blah", "blah", "blah", "blah", "blah", policySet1,
                now.AddDays(-4).LocalDateTime,
                "C:\\packages\\Acme.1.2.0.nupkg", "C:\\Applications\\Acme.1.2.0", null, true);

            // Deployed 3 days prior to 'now'
            threeDayOldDeployment = new JournalEntry("threeDayOld", "blah", "blah", "blah", "blah", "blah", policySet1,
                now.AddDays(-3).LocalDateTime,
                "C:\\packages\\Acme.1.1.0.nupkg", "C:\\Applications\\Acme.1.1.0", null, true);

            // Deployed 2 days prior to 'now'
            twoDayOldDeployment = new JournalEntry("twoDayOld", "blah", "blah", "blah", "blah", "blah", policySet1,
                now.AddDays(-2).LocalDateTime,
                "C:\\packages\\Acme.1.2.0.nupkg", "C:\\Applications\\Acme.1.2.0", null, true);

            // Deployed (unsuccessfully) 1 day prior to 'now'
            oneDayOldUnsuccessfulDeployment = new JournalEntry("oneDayOldUnsuccessful", "blah", "blah", "blah", "blah", "blah", policySet1,
                now.AddDays(-1).LocalDateTime,
                "C:\\packages\\Acme.1.3.0.nupkg", "C:\\Applications\\Acme.1.3.0", null, false);

            // Deployed 5 days prior to 'now', but has a different policy-set
            fiveDayOldNonMatchingDeployment = new JournalEntry("fiveDayOld", "blah", "blah", "blah", "blah", "blah", policySet2,
                now.AddDays(-5).LocalDateTime,
                "C:\\packages\\Beta.1.0.0.nupkg", "C:\\Applications\\Beta.1.0.0", null, true);

            var journalEntries = new List<JournalEntry>
            {
                fiveDayOldNonMatchingDeployment,
                fourDayOldDeployment,
                threeDayOldDeployment,
                twoDayOldDeployment,
                oneDayOldUnsuccessfulDeployment
            };

            deploymentJournal.GetAllJournalEntries().Returns(journalEntries);

            foreach (var journalEntry in journalEntries)
            {
                fileSystem.FileExists(journalEntry.ExtractedFrom).Returns(true);
                fileSystem.DirectoryExists(journalEntry.ExtractedTo).Returns(true);
            }
        }


        [Test]
        public void ShouldNotDeleteDirectoryWhereRetainedDeployedToSame()
        {

#if NET40
            var tf = (TargetFrameworkAttribute)typeof(Program).Assembly.GetCustomAttributes(typeof(TargetFrameworkAttribute), true).First();
#else
            var tf = typeof(Program).GetTypeInfo().Assembly.GetCustomAttribute<TargetFrameworkAttribute>();
#endif
            var journalEntries = new List<JournalEntry>
            {
                fourDayOldDeployment,
                fourDayOldSameLocationDeployment,
                twoDayOldDeployment,
            };
            deploymentJournal.GetAllJournalEntries().Returns(journalEntries);
            fileSystem.FileExists(fourDayOldSameLocationDeployment.ExtractedFrom).Returns(true);
            fileSystem.DirectoryExists(fourDayOldSameLocationDeployment.ExtractedTo).Returns(true);

            const int days = 3;
            retentionPolicy.ApplyRetentionPolicy(policySet1, days, null);

            // Ensure the directories are the same
            Assert.AreEqual(twoDayOldDeployment.ExtractedTo, fourDayOldSameLocationDeployment.ExtractedTo);
            
            // The old directory was not removed...
            fileSystem.DidNotReceive().DeleteDirectory(Arg.Is<string>(s => s.Equals(fourDayOldSameLocationDeployment.ExtractedTo)));

            // ...despite being removed from the journal
            deploymentJournal.Received().RemoveJournalEntries(Arg.Is<IEnumerable<string>>(ids => ids.Contains(fourDayOldSameLocationDeployment.Id)));

            // and unique directory still removed
            fileSystem.Received().DeleteDirectory(Arg.Is<string>(s => s.Equals(fourDayOldDeployment.ExtractedTo)));

        }

        [Test]
        public void ShouldKeepDeploymentsForSpecifiedDays()
        {
            const int days = 3;

            retentionPolicy.ApplyRetentionPolicy(policySet1, days, null);

            // The older artifacts should have been removed
            fileSystem.Received().DeleteDirectory(fourDayOldDeployment.ExtractedTo);
            fileSystem.Received().DeleteFile(fourDayOldDeployment.ExtractedFrom, Arg.Any<FailureOptions>());

            // The newer artifacts, and those from the non-matching policy-set, should have been kept
            // In other words, nothing but the matching deployment should have been removed
            fileSystem.DidNotReceive().DeleteDirectory(Arg.Is<string>(s => !s.Equals(fourDayOldDeployment.ExtractedTo)));
            fileSystem.DidNotReceive().DeleteFile(Arg.Is<string>(s => !s.Equals(fourDayOldDeployment.ExtractedFrom)), Arg.Any<FailureOptions>());

            // The older entry should have been removed from the journal
            deploymentJournal.Received().RemoveJournalEntries(Arg.Is<IEnumerable<string>>(ids => ids.Count() == 1 && ids.Contains(fourDayOldDeployment.Id)));
        }

        [Test]
        public void ShouldKeepSpecifiedNumberOfDeployments()
        {
            // Keep 1 (+ the current) deployment
            // This will not count the unsuccessful deployment
            retentionPolicy.ApplyRetentionPolicy(policySet1, null, 1);

            fileSystem.Received().DeleteDirectory(fourDayOldDeployment.ExtractedTo);
            fileSystem.Received().DeleteFile(fourDayOldDeployment.ExtractedFrom, Arg.Any<FailureOptions>());

            // The newer artifacts, and those from the non-matching policy-set, should have been kept
            // In other words, nothing but the matching deployment should have been removed
            fileSystem.DidNotReceive().DeleteDirectory(Arg.Is<string>(s => !s.Equals(fourDayOldDeployment.ExtractedTo)));
            fileSystem.DidNotReceive().DeleteFile(Arg.Is<string>(s => !s.Equals(fourDayOldDeployment.ExtractedFrom)), Arg.Any<FailureOptions>());

            // The older entry should have been removed from the journal
            deploymentJournal.Received().RemoveJournalEntries(Arg.Is<IEnumerable<string>>(ids => ids.Count() == 1 && ids.Contains(fourDayOldDeployment.Id)));
        }
    }
}