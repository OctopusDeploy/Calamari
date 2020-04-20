using System;
using System.Linq;
using Calamari.Common.Variables;
using Calamari.Deployment;
using Calamari.Tests.Helpers;
using Calamari.Variables;
using FluentAssertions;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Variables
{
    public class VariablesFixture
    {

        [Test]
        public void ShouldLogVariables()
        {
            var variables = new CalamariVariables();
            variables.Set(Common.Variables.SpecialVariables.PrintVariables, true.ToString());
            variables.Set(Common.Variables.SpecialVariables.PrintEvaluatedVariables, true.ToString());
            variables.Set(EnvironmentVariables.Name, "Production");
            const string variableName = "foo";
            const string rawVariableValue = "The environment is #{Octopus.Environment.Name}";
            variables.Set(variableName, rawVariableValue);

            var program = new TestProgram(new InMemoryLog());
            program.VariablesOverride = variables;
            program.RunStubCommand();

            var messages = program.Log.Messages;
            var messagesAsString = string.Join(Environment.NewLine, program.Log.Messages.Select(m => m.FormattedMessage));

            //Assert raw variables were output
            messages.Should().Contain(m => m.Level == InMemoryLog.Level.Warn && m.FormattedMessage == $"{Common.Variables.SpecialVariables.PrintVariables} is enabled. This should only be used for debugging problems with variables, and then disabled again for normal deployments.");
            messagesAsString.Should().Contain("The following variables are available:");
            messagesAsString.Should().Contain($"[{variableName}] = '{rawVariableValue}'");
 
            //Assert evaluated variables were output
            messages.Should().Contain(m => m.Level == InMemoryLog.Level.Warn && m.FormattedMessage == $"{Common.Variables.SpecialVariables.PrintEvaluatedVariables} is enabled. This should only be used for debugging problems with variables, and then disabled again for normal deployments.");
            messagesAsString.Should().Contain("The following evaluated variables are available:");
            messagesAsString.Should().Contain($"[{variableName}] = 'The environment is Production'");
        }
    }
}