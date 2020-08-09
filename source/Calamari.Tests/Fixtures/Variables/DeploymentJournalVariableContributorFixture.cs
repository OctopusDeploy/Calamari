using System;
using System.Collections.Generic;
using Calamari.Common.Features.Deployment.Journal;
using Calamari.Common.Plumbing.Variables;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Variables
{
    [TestFixture]
    public class DeploymentJournalVariableContributorFixture
    {
        IDeploymentJournal journal;
        JournalEntry previous;
        IVariables variables;

        [SetUp]
        public void SetUp()
        {
            journal = Substitute.For<IDeploymentJournal>();
            journal.GetLatestInstallation(Arg.Any<string>()).Returns(_ => previous);
            journal.GetLatestSuccessfulInstallation(Arg.Any<string>()).Returns(_ => previous);
            variables = new CalamariVariables();
        }

        [Test]
        public void ShouldAddVariablesIfPreviousInstallation()
        {
            previous = new JournalEntry("123", "tenant", "env", "proj", "rp01", DateTime.Now, "C:\\App", "C:\\MyApp", false, 
                new List<DeployedPackage>{new DeployedPackage("pkg", "0.0.9", "C:\\PackageOld.nupkg")});
            DeploymentJournalVariableContributor.Previous(variables, journal, "123");
            Assert.That(variables.Get(TentacleVariables.PreviousInstallation.OriginalInstalledPath), Is.EqualTo("C:\\App"));
        }

        [Test]
        public void ShouldAddEmptyVariablesIfNoPreviousInstallation()
        {
            previous = null;
            DeploymentJournalVariableContributor.Previous(variables, journal, "123");
            Assert.That(variables.Get(TentacleVariables.PreviousInstallation.OriginalInstalledPath), Is.EqualTo(""));
        }
        
        [Test]
        public void ShouldAddVariablesIfPreviousSuccessfulInstallation()
        {
            previous = new JournalEntry("123", "tenant", "env", "proj", "rp01", DateTime.Now, "C:\\App", "C:\\MyApp", true, 
                new List<DeployedPackage>{new DeployedPackage("pkg", "0.0.9", "C:\\PackageOld.nupkg")});
            DeploymentJournalVariableContributor.PreviousSuccessful(variables, journal, "123");
            Assert.That(variables.Get(TentacleVariables.PreviousSuccessfulInstallation.OriginalInstalledPath), Is.EqualTo("C:\\App"));
        }

        [Test]
        public void ShouldAddEmptyVariablesIfNoPreviousSuccessfulInstallation()
        {
            previous = null;
            DeploymentJournalVariableContributor.PreviousSuccessful(variables, journal, "123");
            Assert.That(variables.Get(TentacleVariables.PreviousSuccessfulInstallation.OriginalInstalledPath), Is.EqualTo(""));
        }
    }
}
