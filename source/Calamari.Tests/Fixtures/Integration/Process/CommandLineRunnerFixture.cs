using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;
using Calamari.Variables;
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
            var subject = new TestCommandLineRunner(new CalamariVariables());
            var result = subject.Execute(new CommandLineInvocation(executable, "--version"));
            result.HasErrors.Should().BeTrue();
            subject.Output.Errors.Should().Contain(CommandLineRunner.ConstructWin32ExceptionMessage(executable));
        }
    }
}