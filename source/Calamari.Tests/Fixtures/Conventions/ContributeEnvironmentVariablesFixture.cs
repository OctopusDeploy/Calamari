using Calamari.Conventions;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ContributeEnvironmentVariablesFixture
    {
        [Test]
        public void ShouldAddEnvironmentVariables()
        {
            var variables = new VariableDictionary();
            var convention = new ContributeEnvironmentVariablesConvention();
            convention.Install(new RunningDeployment("C:\\Package.nupkg", variables));

            Assert.That(variables.GetNames().Count, Is.GreaterThan(3));
            Assert.That(variables.Evaluate("My OS is #{env:OS}"), Is.StringStarting("My OS is Windows"));
        }
    }


}
