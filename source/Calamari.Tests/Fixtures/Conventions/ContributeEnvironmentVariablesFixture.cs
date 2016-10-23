using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ContributeEnvironmentVariablesConventionFixture
    {
        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldAddWindowsEnvironmentVariables()
        {
            if (!CalamariEnvironment.IsRunningOnWindows)
                Assert.Ignore("This test is designed to run on windows");
            var variables = AddEnvironmentVariables();
            Assert.That(variables.Evaluate("My OS is #{env:OS}"), Does.StartWith("My OS is Windows"));
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        public void ShouldAddLinuxEnvironmentVariables()
        {
            if (!CalamariEnvironment.IsRunningOnNix)
                Assert.Ignore("This test is designed to run on *nix");

            var variables = AddEnvironmentVariables();
            Assert.That(variables.Evaluate("My home starts at #{env:HOME}"), Does.StartWith("My home starts at /home/"));
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldAddMacEnvironmentVariables()
        {
            if (!CalamariEnvironment.IsRunningOnMac)
                Assert.Ignore("This test is designed to run on Mac");
            var variables = AddEnvironmentVariables();
            Assert.That(variables.Evaluate("My home starts at #{env:HOME}"), Does.StartWith("My home starts at /Users/"));
        }

        private VariableDictionary AddEnvironmentVariables()
        {
            var variables = new CalamariVariableDictionary();
            var convention = new ContributeEnvironmentVariablesConvention();
            convention.Install(new RunningDeployment("C:\\Package.nupkg", variables));

            Assert.That(variables.GetNames().Count, Is.GreaterThan(3));
            Assert.That(variables.GetRaw(SpecialVariables.Tentacle.Agent.InstanceName), Is.EqualTo("#{env:TentacleInstanceName}"));
            return variables;
        }
    }
}
