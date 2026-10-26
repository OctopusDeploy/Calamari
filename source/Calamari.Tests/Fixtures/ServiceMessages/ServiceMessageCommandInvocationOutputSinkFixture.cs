using System;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ServiceMessages
{
    [TestFixture]
    public class ServiceMessageCommandInvocationOutputSinkFixture
    {
        IVariables variables;

        [SetUp]
        public void SetUp()
        {
            variables = new CalamariVariables
            {
                { ActionVariables.Name, "Deploy" },
                { MachineVariables.Name, "Web01" },
                { "Greeting", "Hello" }
            };
        }

        [Test]
        public void SetVariable_OnlySetsActionScopedOutputVariables()
        {
            WriteSetVariable(new ServiceMessageCommandInvocationOutputSink(variables), "Foo", "Bar");

            variables.Get("Foo").Should().BeNull();
            variables.Get("Octopus.Action[Deploy].Output.Foo").Should().Be("Bar");
            variables.Get("Octopus.Action[Deploy].Output[Web01].Foo").Should().Be("Bar");
        }

        [Test]
        public void SetVariable_DoesNotOverwriteExistingStepVariable()
        {
            WriteSetVariable(new ServiceMessageCommandInvocationOutputSink(variables), "Greeting", "Goodbye");

            variables.Get("Greeting").Should().Be("Hello");
            variables.Get("Octopus.Action[Deploy].Output.Greeting").Should().Be("Goodbye");
        }

        [Test]
        public void SetVariable_SetsSkipRemainingConventions()
        {
            WriteSetVariable(new ServiceMessageCommandInvocationOutputSink(variables), KnownVariables.Action.SkipRemainingConventions, "True");

            variables.GetFlag(KnownVariables.Action.SkipRemainingConventions).Should().BeTrue();
        }

        [Test]
        public void SetVariable_WhenNameIsAllowed_SetsStepVariable()
        {
            variables.Set(KnownVariables.Calamari.AllowOutputVariableOverwrite, "Bar, Foo");

            var sink = new ServiceMessageCommandInvocationOutputSink(variables);
            WriteSetVariable(sink, "Foo", "Value");
            WriteSetVariable(sink, "Greeting", "Goodbye");

            variables.Get("Foo").Should().Be("Value");
            variables.Get("Greeting").Should().Be("Hello");
        }

        [Test]
        public void SetVariable_CannotAllowNamesFromOutput()
        {
            var sink = new ServiceMessageCommandInvocationOutputSink(variables);

            WriteSetVariable(sink, KnownVariables.Calamari.AllowOutputVariableOverwrite, "Greeting");
            WriteSetVariable(sink, "Greeting", "Goodbye");

            variables.Get("Greeting").Should().Be("Hello");
        }

        [Test]
        public void SetVariable_WhenNameIsTrusted_SetsStepVariable()
        {
            var sink = new ServiceMessageCommandInvocationOutputSink(variables, name => name == "Octopus.Action.AdditionalPaths");

            WriteSetVariable(sink, "Octopus.Action.AdditionalPaths", @"C:\Mount");
            WriteSetVariable(sink, "Greeting", "Goodbye");

            variables.Get("Octopus.Action.AdditionalPaths").Should().Be(@"C:\Mount");
            variables.Get("Octopus.Action[Deploy].Output.Octopus.Action.AdditionalPaths").Should().Be(@"C:\Mount");
            variables.Get("Greeting").Should().Be("Hello");
        }

        static void WriteSetVariable(ServiceMessageCommandInvocationOutputSink sink, string name, string value)
            => sink.WriteInfo(ServiceMessage.Create(ServiceMessageNames.SetVariable.Name, (ServiceMessageNames.SetVariable.NameAttribute, name), (ServiceMessageNames.SetVariable.ValueAttribute, value)).ToString());
    }
}
