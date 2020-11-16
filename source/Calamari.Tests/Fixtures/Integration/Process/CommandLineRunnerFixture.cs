using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
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
            var subject = new TestCommandLineRunner(new InMemoryLog(), new CalamariVariables());
            var result = subject.Execute(new CommandLineInvocation(executable, "--version"));
            result.HasErrors.Should().BeTrue();
            subject.Output.Errors.Should().Contain(CommandLineRunner.ConstructWin32ExceptionMessage(executable));
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ScriptShouldFailWhenTimeoutIsSpecifiedAfterSomeTimeInWindowsSystems()
        {
            // For the sake of those running it with test adapters
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Windows function. See TIMEOUT /?
            const string executable = "TIMEOUT";

            // Script is a timeout for 100 seconds to stimulate a deployment that is stuck
            var invocation = new CommandLineInvocation(
                executable: executable,
                 arguments: "100 /NOBREAK")
            {
                Timeout = TimeSpan.FromMilliseconds(500)
            };

            var subject = new TestCommandLineRunner(new InMemoryLog(), new CalamariVariables());
            var result = subject.Execute(invocation);
            result.HasErrors.Should().BeFalse();
            result.TimedOut.Should().BeTrue();
            subject.Output.Errors.Any(x => x.Contains("Script execution timed out after")).Should().BeTrue();
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void ScriptShouldFailWhenTimeoutIsSpecifiedAfterSomeTimeInNixSystems()
        {
            // For the sake of those running it with test adapters
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Nix function. See 'man sleep' 
            const string executable = "sleep";

            // Script is a timeout for 100 seconds to stimulate a deployment that is stuck
            var invocation = new CommandLineInvocation(
                executable: executable,
                 arguments: "100 /NOBREAK")
            {
                Timeout = TimeSpan.FromMilliseconds(500)
            };

            var subject = new TestCommandLineRunner(new InMemoryLog(), new CalamariVariables());
            var result = subject.Execute(invocation);
            result.HasErrors.Should().BeFalse();
            result.TimedOut.Should().BeTrue();
            subject.Output.Errors.Any(x => x.Contains("Script execution timed out after")).Should().BeTrue();
        }
    }
}