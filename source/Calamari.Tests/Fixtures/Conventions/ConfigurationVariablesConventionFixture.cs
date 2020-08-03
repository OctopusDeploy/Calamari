﻿using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.ConfigurationVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Tests.Helpers;
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

            deployment = new RunningDeployment("C:\\Packages", new CalamariVariables());
            replacer = Substitute.For<IConfigurationVariablesReplacer>();
        }

        [Test]
        public void ShouldNotRunIfVariableNotSet()
        {
            var convention = new ConfigurationVariablesConvention(new ConfigurationVariablesBehaviour(fileSystem, replacer, new InMemoryLog()));
            convention.Install(deployment);
            replacer.DidNotReceiveWithAnyArgs().ModifyConfigurationFile(null, null);
        }

        [Test]
        public void ShouldFindAndCallDeployScripts()
        {
            deployment.Variables.Set(SpecialVariables.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings, "true");
            var convention = new ConfigurationVariablesConvention(new ConfigurationVariablesBehaviour(fileSystem, replacer, new InMemoryLog()));
            convention.Install(deployment);
            replacer.Received().ModifyConfigurationFile("C:\\App\\MyApp\\Web.config", deployment.Variables);
            replacer.Received().ModifyConfigurationFile("C:\\App\\MyApp\\Web.Release.config", deployment.Variables);
            replacer.Received().ModifyConfigurationFile("C:\\App\\MyApp\\Views\\Web.config", deployment.Variables);
        }
    }
}
