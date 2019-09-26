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
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldAddWindowsEnvironmentVariables()
        {
            if (!CalamariEnvironment.IsRunningOnWindows)
                Assert.Ignore("This test is designed to run on windows");
            var variables = AddEnvironmentVariables();
            Assert.That(variables.Evaluate("My OS is #{env:OS}"), Does.StartWith("My OS is Windows"));
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNix)]
        public void ShouldAddLinuxEnvironmentVariables()
        {
            if (!CalamariEnvironment.IsRunningOnNix)
                Assert.Ignore("This test is designed to run on *nix");

            var variables = AddEnvironmentVariables();
            Assert.That(variables.Evaluate("My home starts at #{env:HOME}"), Does.StartWith("My home starts at /home/"));
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyMac)]
        public void ShouldAddMacEnvironmentVariables()
        {
            // Mac running in TeamCity agent service does not contain $HOME variable
            // $PATH is being used since it should be common between service & development
            // http://askubuntu.com/a/394330
            if (!CalamariEnvironment.IsRunningOnMac)
                Assert.Ignore("This test is designed to run on Mac");
            var variables = AddEnvironmentVariables();
            Assert.That(variables.Evaluate("My paths are #{env:PATH}"), Does.Contain("/usr/local/bin"));
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
