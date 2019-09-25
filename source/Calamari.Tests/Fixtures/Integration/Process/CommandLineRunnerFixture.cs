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
            var output = new CaptureCommandOutput();
            var subject = new CommandLineRunner(output);
            var result = subject.Execute(new CommandLineInvocation(executable: "TestingCalamariThisExecutableShouldNeverExist", arguments:"--version"));
            result.HasErrors.Should().BeTrue();
            output.Errors.Should().Contain("TestingCalamariThisExecutableShouldNeverExist was not found, please ensure that TestingCalamariThisExecutableShouldNeverExist is installed and is in the PATH");
        }
    }
}