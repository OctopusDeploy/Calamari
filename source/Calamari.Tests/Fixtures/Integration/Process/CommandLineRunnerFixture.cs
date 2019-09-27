using Calamari.Integration.Processes;
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
            var output = new CaptureCommandOutput();
            var subject = new CommandLineRunner(output);
            var result = subject.Execute(new CommandLineInvocation(executable: executable, arguments:"--version"));
            result.HasErrors.Should().BeTrue();
            output.Errors.Should().Contain(CommandLineRunner.ConstructWin32ExceptionMessage(executable));
        }
    }
}