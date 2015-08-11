using System.IO;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Azure.Tests.Conventions
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.Windows)]
    public class ChooseAzureCloudServiceConfigurationFileConventionFixture
    {
        ICalamariFileSystem fileSystem;
        RunningDeployment deployment;
        VariableDictionary variables;
        ChooseCloudServiceConfigurationFileConvention convention;
        const string StagingDirectory = "C:\\Applications\\Foo";

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            variables = new VariableDictionary();
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, StagingDirectory);
            deployment = new RunningDeployment(StagingDirectory, variables);

            convention = new ChooseCloudServiceConfigurationFileConvention(fileSystem);
        }

        [Test]
        [TestCase("MyCustomCloud.cscfg")]
        [TestCase("CloudService\\MyCustomCloud.cscfg")]
        [TestCase("\\CloudService\\MyCustomCloud.cscfg")]
        [TestCase(".\\CloudService\\MyCustomCloud.cscfg")]
        public void ShouldUseUserSpecifiedConfigurationFile(string customRelativePath)
        {
            variables.Set(SpecialVariables.Action.Azure.CloudServiceConfigurationFileRelativePath, customRelativePath);
            var expectedAbsolutePath = Path.Combine(StagingDirectory, customRelativePath);
            fileSystem.FileExists(expectedAbsolutePath).Returns(true);

            convention.Install(deployment);

            Assert.That(GetResolvedPathFromVariables(), Is.EqualTo(expectedAbsolutePath));
        }

        [Test]
        [TestCase("Production")]
        [TestCase("Staging")]
        [TestCase("SomeOtherEnvironment")]
        [TestCase("What if my environment has spaces")]
        public void IfNoUserSpecifiedConfigThenShouldUseEnvironmentConfigurationFile(string environment)
        {
            variables.Set(SpecialVariables.Environment.Name, environment);
            var expectedAbsolutePath = Path.Combine(StagingDirectory, string.Format("ServiceConfiguration.{0}.cscfg", environment));
            fileSystem.FileExists(expectedAbsolutePath).Returns(true);

            convention.Install(deployment);

            Assert.That(GetResolvedPathFromVariables(), Is.EqualTo(expectedAbsolutePath));
        }

        [Test]
        public void IfNoUserSpecifiedOrEnvironmentFileThenShouldUseDefault()
        {
            var expectedAbsolutePath = Path.Combine(StagingDirectory, "ServiceConfiguration.Cloud.cscfg");
            fileSystem.FileExists(expectedAbsolutePath).Returns(true);

            convention.Install(deployment);

            Assert.That(GetResolvedPathFromVariables(), Is.EqualTo(expectedAbsolutePath));
        }

        string GetResolvedPathFromVariables()
        {
            return deployment.Variables.Get(SpecialVariables.Action.Azure.Output.ConfigurationFile);
        }
    }
}