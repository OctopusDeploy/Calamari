using System;
using System.IO;
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
            var currentWorkingDirectory = Path.GetDirectoryName(GetType().Assembly.FullLocalPath());
            var octoDiffExe = Path.Combine(currentWorkingDirectory, "Octodiff.exe");
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

        protected string MapSamplePath(string file)
        {
            var parent = Path.GetDirectoryName(GetType().Assembly.FullLocalPath());
            var path = GetType().Namespace.Replace("Calamari.Tests", "");
            path = path.Replace(".", "\\").Trim('.', '\\');
            return Path.GetFullPath(Path.Combine(parent, path, file));
        }

        protected static string GetPackageDownloadFolder(string fixtureName)
        {
            string currentDirectory = typeof(CalamariFixture).Assembly.FullLocalPath();
            string targetFolder = "source\\";
            int index = currentDirectory.LastIndexOf(targetFolder, StringComparison.OrdinalIgnoreCase);
            string solutionRoot = currentDirectory.Substring(0, index + targetFolder.Length);

            var packageDirectory = Path.Combine(solutionRoot, "Calamari.Tests\\bin\\Fixtures", fixtureName);

            return packageDirectory;
        }
    }
}