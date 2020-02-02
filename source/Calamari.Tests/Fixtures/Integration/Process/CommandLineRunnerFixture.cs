using Calamari.Deployment;
using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ScriptShouldFailWhenTimeoutIsSpecifiedAfterSomeTimeInWindowsSystems()
        {
            // For the sake of those running it with test adapters
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return;
            }

            // Windows function. See TIMEOUT /?
            const string executable = "TIMEOUT";
            var output = new CaptureCommandOutput();
            var subject = new CommandLineRunner(output);

            // Set a timeout of 500 milliseconds
            var environmentVars = new Dictionary<string, string> { };
            var timeout = TimeSpan.FromMilliseconds(500);

            // Run a function with a 100 second timeout, which should exit before it completes due to the configured timeout.
            // This simulates stuck deployments on a larger scale (say stuck for 24 hours).
            var result = subject.Execute(new CommandLineInvocation(executable: executable, arguments: "100 /NOBREAK", environmentVars: environmentVars, timeout: timeout));
            result.HasErrors.Should().BeFalse();
            result.TimedOut.Should().BeTrue();
            output.Errors.Should().BeEmpty();
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

            // *Nix function. See `man sleep`
            const string executable = "sleep";
            var output = new CaptureCommandOutput();
            var subject = new CommandLineRunner(output);

            // Set a timeout of 500 milliseconds
            var environmentVars = new Dictionary<string, string> { };
            var timeout = TimeSpan.FromMilliseconds(500);

            // Run a function with a 100 second timeout, which should exit before it completes due to the configured timeout.
            // This simulates stuck deployments on a larger scale (say stuck for 24 hours).
            var result = subject.Execute(new CommandLineInvocation(executable: executable, arguments: "100", environmentVars: environmentVars, timeout: timeout));
            result.HasErrors.Should().BeFalse();
            result.TimedOut.Should().BeTrue();
            output.Errors.Should().BeEmpty();
        }
    }
}