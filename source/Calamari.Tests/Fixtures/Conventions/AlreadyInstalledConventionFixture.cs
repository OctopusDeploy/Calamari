using System;
<<<<<<< HEAD
using System.Threading;
using Calamari.Commands;
=======
using System.Collections.Generic;
>>>>>>> vNext
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Deployment.Journal;
using Calamari.Integration.Processes;
using Calamari.Shared;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class AlreadyInstalledConventionFixture
    {
        JournalEntry previous;
        IDeploymentJournal journal;
        CalamariVariableDictionary variables;

        [SetUp]
        public void SetUp()
        {
            journal = Substitute.For<IDeploymentJournal>();
            journal.GetLatestInstallation(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(_ => previous);
            variables = new CalamariVariableDictionary();
        }

        [Test]
        public void ShouldSkipIfInstalled()
        {
            variables.Set(SpecialVariables.Package.SkipIfAlreadyInstalled, true.ToString());
            previous = new JournalEntry("123", "tenant", "env", "proj", "rp01", DateTime.Now, "C:\\App", "C:\\MyApp", true, 
                new List<DeployedPackage>{new DeployedPackage("pkg", "0.0.9", "C:\\PackageOld.nupkg")});

            RunConvention();

            Assert.That(variables.Get(SpecialVariables.Action.SkipJournal), Is.EqualTo("true"));
        }

        [Test]
        public void ShouldOnlySkipIfSpecified()
        {
            previous = new JournalEntry("123", "tenant", "env", "proj", "rp01", DateTime.Now, "C:\\App", "C:\\MyApp", true, 
                new List<DeployedPackage>{new DeployedPackage("pkg", "0.0.9", "C:\\PackageOld.nupkg")});

            RunConvention();

            Assert.That(variables.Get(SpecialVariables.Action.SkipJournal), Is.Null);
        }

        [Test]
        public void ShouldNotSkipIfPreviouslyFailed()
        {
            variables.Set(SpecialVariables.Package.SkipIfAlreadyInstalled, true.ToString());
            previous = new JournalEntry("123", "tenant", "env", "proj", "rp01", DateTime.Now, "C:\\App", "C:\\MyApp", false, 
                new List<DeployedPackage>{new DeployedPackage("pkg", "0.0.9", "C:\\PackageOld.nupkg")});

            RunConvention();

            Assert.That(variables.Get(SpecialVariables.Action.SkipJournal), Is.Null);
        }

        void RunConvention()
        {
            var convention = new AlreadyInstalledConvention(journal);
            convention.Run(new CalamariExecutionContext("C:\\Package.nupkg", variables));
        }
    }
}