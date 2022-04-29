using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Processes;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process
{
    [TestFixture]
    public class CommandLineRunnerFixture
    {
        [Test]
        public void ScriptShouldFailIfExecutableDoesNotExist()
        {
            const string executable = "TestingCalamariThisExecutableShouldNeverExist";
            var subject = new TestCommandLineRunner(new InMemoryLog(), new CalamariVariables());
            var result = subject.Execute(new CommandLineInvocation(executable, "--version"));
            result.HasErrors.Should().BeTrue();
            subject.Output.Errors.Should().Contain(CommandLineRunner.ConstructWin32ExceptionMessage(executable));
        }
    }
}