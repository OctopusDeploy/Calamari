using System;
using System.Linq;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Variables
{
    public class VariablesFixture
    {

        [Test]
        public void ShouldLogVariables()
        {
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.PrintVariables, true.ToString());
            variables.Set(KnownVariables.PrintEvaluatedVariables, true.ToString());
            variables.Set(DeploymentEnvironment.Name, "Production");
            const string variableName = "foo";
            const string rawVariableValue = "The environment is #{Octopus.Environment.Name}";
            variables.Set(variableName, rawVariableValue);

            var program = new TestCalamariRunner(new InMemoryLog());
            program.VariablesOverride = variables;
            program.RunStubCommand();

            var messages = program.TestLog.Messages;
            var messagesAsString = string.Join(Environment.NewLine, program.TestLog.Messages.Select(m => m.FormattedMessage));

            //Assert raw variables were output
            messages.Should().Contain(m => m.Level == InMemoryLog.Level.Warn && m.FormattedMessage == $"{KnownVariables.PrintVariables} is enabled. This should only be used for debugging problems with variables, and then disabled again for normal deployments.");
            messagesAsString.Should().Contain("The following variables are available:");
            messagesAsString.Should().Contain($"[{variableName}] = '{rawVariableValue}'");

            //Assert evaluated variables were output
            messages.Should().Contain(m => m.Level == InMemoryLog.Level.Warn && m.FormattedMessage == $"{KnownVariables.PrintEvaluatedVariables} is enabled. This should only be used for debugging problems with variables, and then disabled again for normal deployments.");
            messagesAsString.Should().Contain("The following evaluated variables are available:");
            messagesAsString.Should().Contain($"[{variableName}] = 'The environment is Production'");
        }
    }
}