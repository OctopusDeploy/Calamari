using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.LaunchTools;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using System;

namespace Calamari.Tests.LaunchTools
{
    [TestFixture]
    public class NodeExecutorFixture
    {
        [Test]
        [TestCase("execute")]
        [TestCase("Execute")]
        [TestCase("eXeCuTe")]
        public void Execute_UsesCorrectCommandLineInvocation_ForValidExecutionCommands(string executionCommand)
        {
            // Arrange
            var instructions = BuildInstructions(executionCommand);
            var variables = BuildVariables();
            var options = BuildOptions();
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            var sut = new NodeExecutor(options, variables, commandLineRunner, new InMemoryLog());
            CommandLineInvocation capturedInvocation = null;
            var fakeResult = new CommandResult("fakeCommand", 0);
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(arg => capturedInvocation = arg)).Returns(fakeResult);

            // Act
            sut.Execute(instructions);

            // Assert
            var arguments = capturedInvocation.Arguments.Split(' ');
            arguments.Should().HaveCount(8)
                .And.HaveElementAt(0, "\"expectedBootstrapperPath\\bootstrapper.js\"")
                .And.HaveElementAt(1, $"\"{executionCommand}\"")
                .And.HaveElementAt(2, "\"expectedTargetPath\"")
                .And.HaveElementAt(4, "\"encryptionPassword\"")
                .And.HaveElementAt(5, "\"Octopuss\"")
                .And.HaveElementAt(6, "\"inputsKey\"")
                .And.HaveElementAt(7, "\"targetInputsKey\"");
        }

        [TestCase("discover")]
        [TestCase("Discover")]
        [TestCase("dIsCoVeR")]
        public void Execute_UsesCorrectCommandLineInvocation_ForValidTargetDiscoveryCommands(string discoveryCommand)
        {
            // Arrange
            var instructions = BuildInstructions(discoveryCommand);
            var variables = BuildVariables();
            var options = BuildOptions();
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            var sut = new NodeExecutor(options, variables, commandLineRunner, new InMemoryLog());
            CommandLineInvocation capturedInvocation = null;
            var fakeResult = new CommandResult("fakeCommand", 0);
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(arg => capturedInvocation = arg)).Returns(fakeResult);

            // Act
            sut.Execute(instructions);

            // Assert
            var arguments = capturedInvocation.Arguments.Split(' ');
            arguments.Should().HaveCount(7)
                .And.HaveElementAt(0, "\"expectedBootstrapperPath\\bootstrapper.js\"")
                .And.HaveElementAt(1, $"\"{discoveryCommand}\"")
                .And.HaveElementAt(2, "\"expectedTargetPath\"")
                .And.HaveElementAt(4, "\"encryptionPassword\"")
                .And.HaveElementAt(5, "\"Octopuss\"")
                .And.HaveElementAt(6, "\"discoveryContextKey\"");
        }

        [Test]
        public void Execute_ThrowsCommandException_ForUnknownBootstrapperInvocationCommand()
        {
            // Arrange
            var instructions = BuildInstructions("wrongCommand");
            var variables = BuildVariables();
            var options = BuildOptions();
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            var sut = new NodeExecutor(options, variables, commandLineRunner, new InMemoryLog());

            // Act
            Action action = () => sut.Execute(instructions);

            // Assert
            action.Should().Throw<CommandException>().WithMessage("Unknown bootstrapper invocation command: 'wrongCommand'");
        }



        private string BuildInstructions(string command) => $@"{{
    ""nodePathVariable"": ""nodePathKey"",
    ""targetPathVariable"": ""targetPathKey"",
    ""bootstrapperPathVariable"": ""bootstrapperVarKey"",
    ""bootstrapperInvocationCommand"": ""{command}"",
    ""inputsVariable"": ""inputsKey"",
    ""deploymentTargetInputsVariable"": ""targetInputsKey"",
    ""targetDiscoveryContextVariable"": ""discoveryContextKey""
}}";

        private CommonOptions BuildOptions() => CommonOptions.Parse(new[]
            {
                "Test",
                "--variables",
                "firstInsensitiveVariablesFileName",
                "--sensitiveVariables",
                "firstSensitiveVariablesFileName",
                "--sensitiveVariables",
                "secondSensitiveVariablesFileName",
                "--sensitiveVariablesPassword",
                "encryptionPassword"
            });

        private CalamariVariables BuildVariables()
        {
            var variables = new CalamariVariables();
            variables.Set("nodePathKey", "expectedNodePath");
            variables.Set("targetPathKey", "expectedTargetPath");
            variables.Set("bootstrapperVarKey", "expectedBootstrapperPath");
            variables.Set("inputsKey", @"{ input: ""foo"" }");
            variables.Set("targetInputsKey", @"{ targetInput: ""bar"" }");
            variables.Set("foo", "AwsAccount");
            variables.Set("discoveryContextKey", @"{ ""blah"": ""baz"" }");
            return variables;
        }
    }
}
