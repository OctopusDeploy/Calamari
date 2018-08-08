using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.ConfigurationVariables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Shared;
using Calamari.Shared.FileSystem;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ConfigurationVariablesConventionFixture
    {
        RunningDeployment deployment;
        ICalamariFileSystem fileSystem;
        IConfigurationVariablesReplacer replacer;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.EnumerateFilesRecursively(Arg.Any<string>(), Arg.Any<string[]>()).Returns(new[]
            {
                "C:\\App\\MyApp\\Web.config",
                "C:\\App\\MyApp\\Web.Release.config",
                "C:\\App\\MyApp\\Views\\Web.config"
            });

            deployment = new RunningDeployment("C:\\Packages", new CalamariVariableDictionary());
            replacer = Substitute.For<IConfigurationVariablesReplacer>();
        }

        [Test]
        public void ShouldNotRunIfVariableNotSet()
        {
            var convention = new ConfigurationVariablesConvention(fileSystem, replacer);
            convention.Install(deployment);
            replacer.DidNotReceiveWithAnyArgs().ModifyConfigurationFile(null, null);
        }

        [Test]
        public void ShouldFindAndCallDeployScripts()
        {
            deployment.Variables.Set(SpecialVariables.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings, "true");
            var convention = new ConfigurationVariablesConvention(fileSystem, replacer);
            convention.Install(deployment);
            replacer.Received().ModifyConfigurationFile("C:\\App\\MyApp\\Web.config", deployment.Variables);
            replacer.Received().ModifyConfigurationFile("C:\\App\\MyApp\\Web.Release.config", deployment.Variables);
            replacer.Received().ModifyConfigurationFile("C:\\App\\MyApp\\Views\\Web.config", deployment.Variables);
        }
    }
}
