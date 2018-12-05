using System;
using System.Linq;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting.Python;
using Calamari.Integration.ServiceMessages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Octopus.CoreUtilities;
using Octostache;

namespace Calamari.Tests.Fixtures
{
    public class RequiresMinimumPython3VersionAttribute : TestAttribute, ITestAction
    {
        const int Major = 3;
        readonly int minor;

        public RequiresMinimumPython3VersionAttribute(int minor)
        {
            this.minor = minor;
        }

        public void BeforeTest(ITest test)
        {
            var python3Version = GetPython3Version();
            if (python3Version.None())
            {
                Assert.Ignore("Requires Python3 to be installed");
            }
            if (python3Version.Value < new Version(Major, minor))
            {
                Assert.Ignore($"Requires Python3 {Major}.{minor}");
            }
        }

        public void AfterTest(ITest test)
        {
        }

        public ActionTargets Targets { get; set; }

        static Maybe<Version> GetPython3Version()
        {
            var executable = PythonBootstrapper.FindPythonExecutable();
            var command = new CommandLine(executable).Argument("--version");
            var capture = new CaptureCommandOutput();
            var runner = new CommandLineRunner(
                new SplitCommandOutput(
                    new ConsoleCommandOutput(),
                    new ServiceMessageCommandOutput(new VariableDictionary()),
                    capture));
            var result = runner.Execute(command.Build());
            if (result.ExitCode != 0)
                return Maybe<Version>.None;

            var resultVersionString = capture.AllMessages.Single().Replace("Python ", "");
            return Version.Parse(resultVersionString).AsSome();
        }
    }
}