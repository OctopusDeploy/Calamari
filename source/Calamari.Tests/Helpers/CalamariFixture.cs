using System;
using System.IO;
using ApprovalTests.Namers;
using ApprovalTests.Reporters;
using Calamari.Commands;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using Octostache;

namespace Calamari.Tests.Helpers
{
    [UseReporter(typeof(DiffReporter))]
    [UseApprovalSubdirectory("Approved")]
    public abstract class CalamariFixture
    {
        protected CommandLine Calamari()
        {
            var calamariFullPath = typeof (DeployPackageCommand).Assembly.FullLocalPath();
            var calamariConfigFilePath = calamariFullPath + ".config";
            if (!File.Exists(calamariConfigFilePath))
                throw new FileNotFoundException($"Unable to find {calamariConfigFilePath} which means the config file would not have been included in testing {calamariFullPath}");

            return CommandLine.Execute(calamariFullPath);
        }

        protected CommandLine OctoDiff()
        {
            var octoDiffExe = Path.Combine(TestEnvironment.CurrentWorkingDirectory, "Octodiff.exe");
            if (!File.Exists(octoDiffExe))
                throw new FileNotFoundException($"Unable to find {octoDiffExe}");

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