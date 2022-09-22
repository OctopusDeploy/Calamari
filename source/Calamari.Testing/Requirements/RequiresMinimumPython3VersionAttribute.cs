using System;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting.Python;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Octopus.CoreUtilities;

namespace Calamari.Testing.Requirements
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
                Assert.Inconclusive("Requires Python3 to be installed");
            }
            if (python3Version.Value < new Version(Major, minor))
            {
                Assert.Inconclusive($"Requires Python3 {Major}.{minor}");
            }
        }

        public void AfterTest(ITest test)
        {
        }

        public ActionTargets Targets { get; set; }

        static readonly Regex PythonVersionFinder = new Regex(@"Python (\d*)\.(\d*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static Maybe<Version> GetPython3Version()
        {
            var executable = PythonBootstrapper.FindPythonExecutable();
            var command = new CommandLine(executable).Argument("--version");
            var runner = new TestCommandLineRunner(new InMemoryLog(), new CalamariVariables());
            var result = runner.Execute(command.Build());
            if (result.ExitCode != 0)
                return Maybe<Version>.None;

            var allCapturedMessages = runner.Output.AllMessages.Aggregate((a, b) => $"{a}, {b}");
            var pythonVersionMatch = PythonVersionFinder.Match(allCapturedMessages);
            if (!pythonVersionMatch.Success)
                return Maybe<Version>.None;

            var major = pythonVersionMatch.Groups[1].Value;
            var minor = pythonVersionMatch.Groups[2].Value;
            return new Version(int.Parse(major), int.Parse(minor)).AsSome();
        }
    }
}