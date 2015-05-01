using System;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.All)]
    public class AlreadyInstalledConventionFixture
    {
        JournalEntry previous;
        IDeploymentJournal journal;
        VariableDictionary variables;

        [SetUp]
        public void SetUp()
        {
            journal = Substitute.For<IDeploymentJournal>();
            journal.GetLatestInstallation(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(_ => previous);
            variables = new VariableDictionary();
        }

        [Test]
        public void ShouldSkipIfInstalled()
        {
            variables.Set(SpecialVariables.Package.SkipIfAlreadyInstalled, true.ToString());
            previous = new JournalEntry("123", "env", "proj", "pkg", "0.0.9", "rp01", DateTime.Now, "C:\\PackageOld.nupkg", "C:\\App", "C:\\MyApp", true);

            RunConvention();

            Assert.That(variables.Get(SpecialVariables.Action.SkipJournal), Is.EqualTo("true"));
        }

        [Test]
        public void ShouldOnlySkipIfSpecified()
        {
            previous = new JournalEntry("123", "env", "proj", "pkg", "0.0.9", "rp01", DateTime.Now, "C:\\PackageOld.nupkg", "C:\\App", "C:\\MyApp", true);

            RunConvention();

            Assert.That(variables.Get(SpecialVariables.Action.SkipJournal), Is.Null);
        }

        [Test]
        public void ShouldNotSkipIfPreviouslyFailed()
        {
            variables.Set(SpecialVariables.Package.SkipIfAlreadyInstalled, true.ToString());
            previous = new JournalEntry("123", "env", "proj", "pkg", "0.0.9", "rp01", DateTime.Now, "C:\\PackageOld.nupkg", "C:\\App", "C:\\MyApp", false);

            RunConvention();

            Assert.That(variables.Get(SpecialVariables.Action.SkipJournal), Is.Null);
        }

        void RunConvention()
        {
            var convention = new AlreadyInstalledConvention(journal);
            convention.Install(new RunningDeployment("C:\\Package.nupkg", variables));
        }
    }
}