using System;
using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Integration.Processes;
using Calamari.Variables;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ContributePreviousInstallationConventionFixture
    {
        IDeploymentJournal journal;
        JournalEntry previous;
        IVariables variables;

        [SetUp]
        public void SetUp()
        {
            journal = Substitute.For<IDeploymentJournal>();
            journal.GetLatestInstallation(Arg.Any<string>()).Returns(_ => previous);
            variables = new CalamariVariables();
        }

        [Test]
        public void ShouldAddVariablesIfPreviousInstallation()
        {
            previous = new JournalEntry("123", "tenant", "env", "proj", "rp01", DateTime.Now, "C:\\App", "C:\\MyApp", true, 
                new List<DeployedPackage>{new DeployedPackage("pkg", "0.0.9", "C:\\PackageOld.nupkg")});
            RunConvention();
            Assert.That(variables.Get(SpecialVariables.Tentacle.PreviousInstallation.OriginalInstalledPath), Is.EqualTo("C:\\App"));
        }

        [Test]
        public void ShouldAddEmptyVariablesIfNoPreviousInstallation()
        {
            previous = null;
            RunConvention();
            Assert.That(variables.Get(SpecialVariables.Tentacle.PreviousInstallation.OriginalInstalledPath), Is.EqualTo(""));
        }

        void RunConvention()
        {
            var convention = new ContributePreviousInstallationConvention(journal);
            convention.Install(new RunningDeployment("C:\\Package.nupkg", variables));
        }
    }
}
