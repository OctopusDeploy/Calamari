using System;
using System.IO;
using System.Linq;
using Calamari.Commands;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using Octostache;

namespace Calamari.Tests.Helpers
{
    public abstract class CalamariFixture
    {
        protected CommandLine Calamari()
        {
            return CommandLine.Execute(typeof (DeployPackageCommand).Assembly.FullLocalPath());
        }

        protected CommandLine OctoDiff()
        {
            var octoDiffExe = Path.Combine(TestEnvironment.CurrentWorkingDirectory, "Octodiff.exe");
            if (!File.Exists(octoDiffExe))
                throw new FileNotFoundException("Unable to find Octodiff.exe");

            return CommandLine.Execute(octoDiffExe);
        }

        protected CalamariResult Invoke(CommandLine command, VariableDictionary variables)
        {
            var capture = new CaptureCommandOutput();
            var runner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables), capture));
            var result = runner.Execute(command.Build());
            return new CalamariResult(result.ExitCode, capture);
        }

        protected CalamariResult Invoke(CommandLine command)
        {
            return Invoke(command, new VariableDictionary());
        }

        protected string GetFixtureResouce(params string[] paths)
        {
            var path = GetType().Namespace.Replace("Calamari.Tests.", String.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            return Path.Combine(TestEnvironment.CurrentWorkingDirectory, path, Path.Combine(paths));
        }
    }
}