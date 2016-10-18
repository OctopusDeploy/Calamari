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
            var variables = AddEnvironmentVariables();
            WindowsEnvironmentVariableTest(variables);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        public void ShouldAddLinuxEnvironmentVariables()
        {
            var variables = AddEnvironmentVariables();
            LinuxEnvironmentVariableTest(variables);
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

        private void WindowsEnvironmentVariableTest(VariableDictionary variables)
        {
            Assert.That(variables.Evaluate("My OS is #{env:OS}"), Is.StringStarting("My OS is Windows"));
        }

        private void LinuxEnvironmentVariableTest(VariableDictionary variables)
        {
            Assert.That(variables.Evaluate("My home starts at #{env:HOME}"), Is.StringStarting("My home starts at /home"));
        }

    }
}
