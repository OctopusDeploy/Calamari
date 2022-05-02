using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment.Journal;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Conventions
{
    [TestFixture]
    public class AlreadyInstalledConventionFixture
    {
        JournalEntry previous;
        IDeploymentJournal journal;
        IVariables variables;

        [SetUp]
        public void SetUp()
        {
            journal = Substitute.For<IDeploymentJournal>();
            journal.GetLatestInstallation(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(_ => previous);
            variables = new CalamariVariables();
        }

        [Test]
        public void ShouldSkipIfInstalled()
        {
            variables.Set(KnownVariables.Package.SkipIfAlreadyInstalled, true.ToString());
            previous = new JournalEntry("123", "tenant", "env", "proj", "rp01", DateTime.Now, "C:\\App", "C:\\MyApp", true,
                new List<DeployedPackage>{new DeployedPackage("pkg", "0.0.9", "C:\\PackageOld.nupkg")});

            RunConvention();

            Assert.That(variables.Get(KnownVariables.Action.SkipJournal), Is.EqualTo("true"));
        }

        [Test]
        public void ShouldOnlySkipIfSpecified()
        {
            previous = new JournalEntry("123", "tenant", "env", "proj", "rp01", DateTime.Now, "C:\\App", "C:\\MyApp", true,
                new List<DeployedPackage>{new DeployedPackage("pkg", "0.0.9", "C:\\PackageOld.nupkg")});

            RunConvention();

            Assert.That(variables.Get(KnownVariables.Action.SkipJournal), Is.Null);
        }

        [Test]
        public void ShouldNotSkipIfPreviouslyFailed()
        {
            variables.Set(KnownVariables.Package.SkipIfAlreadyInstalled, true.ToString());
            previous = new JournalEntry("123", "tenant", "env", "proj", "rp01", DateTime.Now, "C:\\App", "C:\\MyApp", false,
                new List<DeployedPackage>{new DeployedPackage("pkg", "0.0.9", "C:\\PackageOld.nupkg")});

            RunConvention();

            Assert.That(variables.Get(KnownVariables.Action.SkipJournal), Is.Null);
        }

        void RunConvention()
        {
            var convention = new AlreadyInstalledConvention(new InMemoryLog(), journal);
            convention.Install(new RunningDeployment("C:\\Package.nupkg", variables));
        }
    }
}