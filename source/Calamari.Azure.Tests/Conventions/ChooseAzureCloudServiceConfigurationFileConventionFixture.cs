using System.IO;
using Calamari.Azure.Deployment.Conventions;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Tests.Fixtures.ScriptCS;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Azure.Tests.Conventions
{
    [TestFixture]
    public class ChooseAzureCloudServiceConfigurationFileConventionFixture
    {
        ICalamariFileSystem fileSystem;
        RunningDeployment deployment;
        CalamariVariableDictionary variables;
        ChooseCloudServiceConfigurationFileConvention convention;
        const string StagingDirectory = "C:\\Applications\\Foo";

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            variables = new CalamariVariableDictionary();
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, StagingDirectory);
            deployment = new RunningDeployment(StagingDirectory, variables);

            convention = new ChooseCloudServiceConfigurationFileConvention(fileSystem);
        }

        [PlatformTest(CompatablePlatform.Windows)]
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

        [PlatformTest(CompatablePlatform.Windows)]
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

        [PlatformTest(CompatablePlatform.Windows)]
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