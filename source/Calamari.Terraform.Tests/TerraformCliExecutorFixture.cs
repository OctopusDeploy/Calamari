using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Terraform.Tests
{
    public class TerraformCliExecutorFixture
    {
        IVariables variables;
        TerraformCliExecutor cliExecutor;

        [SetUp]
        public void Setup()
        {
            variables = Substitute.For<IVariables>();
            variables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation => invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v0.15.0")))
                             .Returns(new CommandResult("foo", 0));
            cliExecutor = new TerraformCliExecutor(Substitute.For<ILog>(),
                                                   Substitute.For<ICalamariFileSystem>(),
                                                   commandLineRunner,
                                                   new RunningDeployment("blah", variables),
                                                   new Dictionary<string, string>());
        }

        [Test]
        public void TerraformVariableFiles_Null()
        {
            variables.Get(TerraformSpecialVariables.Action.Terraform.VarFiles).Returns((string)null);
            cliExecutor.TerraformVariableFiles.Should().BeNull();
        }

        [Test]
        public void TerraformVariableFiles_SingleLine()
        {
            variables.Get(TerraformSpecialVariables.Action.Terraform.VarFiles).Returns("foo");
            cliExecutor.TerraformVariableFiles.Should().Be("-var-file=\"foo\"");
        }

        [Test]
        public void TerraformVariableFiles_MultiLine()
        {
            variables.Get(TerraformSpecialVariables.Action.Terraform.VarFiles).Returns("foo\nbar\r\nbaz");
            cliExecutor.TerraformVariableFiles.Should().Be("-var-file=\"foo\" -var-file=\"bar\" -var-file=\"baz\"");
        }

        [TestCase("│ Error while installing dnsimple/dnsimple v2.0.1: unsuccessful request to\n│ https://github.com/...\n│ 502 Bad Gateway", true)]
        [TestCase("│ Error while installing hashicorp/azurerm v4.63.0: unsuccessful request to\n│ https://releases.hashicorp.com/...\n│ 503 Service Unavailable", true)]
        [TestCase("Error configuring backend: 502 Bad Gateway", true)]
        [TestCase("Error: Invalid backend configuration", false)]
        [TestCase("Error: No valid credential sources found", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsTransientInitError_MatchesExpectedPatterns(string errorText, bool expected)
        {
            TerraformCliExecutor.IsTransientInitError(errorText).Should().Be(expected);
        }

        [Test]
        public void InitializePlugins_RetriesOnTransient502Error()
        {
            var testVariables = Substitute.For<IVariables>();
            testVariables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            var callCount = 0;
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation =>
            {
                callCount++;
                if (callCount == 1)
                    invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v0.15.0");
            })).Returns(_ =>
            {
                if (callCount == 2)
                    return new CommandResult("terraform init", 1, "│ Error while installing dnsimple/dnsimple v2.0.1: unsuccessful request to\n│ https://github.com/...\n│ 502 Bad Gateway");
                return new CommandResult("foo", 0);
            });

            var executor = new TerraformCliExecutor(Substitute.For<ILog>(), Substitute.For<ICalamariFileSystem>(), commandLineRunner, new RunningDeployment("blah", testVariables), new Dictionary<string, string>());

            commandLineRunner.Received(3).Execute(Arg.Any<CommandLineInvocation>());
        }

        [Test]
        public void InitializePlugins_DoesNotRetryNonTransientError()
        {
            var testVariables = Substitute.For<IVariables>();
            testVariables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            var callCount = 0;
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation =>
            {
                callCount++;
                if (callCount == 1)
                    invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v0.15.0");
            })).Returns(_ =>
            {
                if (callCount == 2)
                    return new CommandResult("terraform init", 1, "Error: Invalid backend configuration");
                return new CommandResult("foo", 0);
            });

            var act = () => new TerraformCliExecutor(Substitute.For<ILog>(), Substitute.For<ICalamariFileSystem>(), commandLineRunner, new RunningDeployment("blah", testVariables), new Dictionary<string, string>());

            act.Should().Throw<CommandLineException>();
            commandLineRunner.Received(2).Execute(Arg.Any<CommandLineInvocation>());
        }

        [Test]
        public void InitializePlugins_ThrowsAfterRetriesExhausted()
        {
            var testVariables = Substitute.For<IVariables>();
            testVariables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            var callCount = 0;
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation =>
            {
                callCount++;
                if (callCount == 1)
                    invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v0.15.0");
            })).Returns(_ =>
            {
                if (callCount >= 2)
                    return new CommandResult("terraform init", 1, "│ Error while installing dnsimple/dnsimple v2.0.1: unsuccessful request to\n│ https://github.com/...\n│ 502 Bad Gateway");
                return new CommandResult("foo", 0);
            });

            var act = () => new TerraformCliExecutor(Substitute.For<ILog>(), Substitute.For<ICalamariFileSystem>(), commandLineRunner, new RunningDeployment("blah", testVariables), new Dictionary<string, string>());

            act.Should().Throw<CommandLineException>();
            commandLineRunner.Received(5).Execute(Arg.Any<CommandLineInvocation>());
        }
    }
}
