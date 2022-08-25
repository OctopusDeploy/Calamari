using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Conventions
{

    [TestFixture]
    public class ConditionalConventionFixture
    {
        public class TestInstallConvention : IInstallConvention
        {
            private readonly Action<RunningDeployment> callback;

            public TestInstallConvention(Action<RunningDeployment> callback)
            {
                this.callback = callback;
            }

            public void Install(RunningDeployment deployment)
            {
                callback(deployment);
            }
        }

        RunningDeployment deployment;

        [SetUp]
        public void SetUp()
        {
            deployment = new RunningDeployment("c:\\packages", new CalamariVariables());
        }

        [Test]
        public void ShouldInstallIfPredicatePasses()
        {
            bool didExecute = false;
            var convention = new TestInstallConvention((_) => didExecute = true).When(_ => true);
            convention.Install(deployment);
            didExecute.Should().BeTrue();
        }

        [Test]
        public void ShouldNotInstallIfPredicateFails()
        {
            bool didExecute = false;
            var convention = new TestInstallConvention((_) => didExecute = true).When(_ => false);
            convention.Install(deployment);
            didExecute.Should().BeFalse();
        }
    }
}
