using System.IO;
using Calamari.Integration.Processes;

namespace Calamari.Tests.Helpers
{
    public abstract class CalamariFixture
    {
        protected CommandLine Calamari()
        {
            return CommandLine.Execute(typeof (CaptureCommandOutput).Assembly.FullLocalPath());
        }

        protected CalamariResult Invoke(CommandLine command)
        {
            var capture = new CaptureCommandOutput();
            var runner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), capture));

            var result = runner.Execute(command.Build());
            return new CalamariResult(result.ExitCode, capture);
        }

        protected string MapSamplePath(string file)
        {
            var parent = Path.GetDirectoryName(GetType().Assembly.FullLocalPath());
            var path = GetType().Namespace.Replace("Calamari.Tests", "");
            path = path.Replace(".", "\\").Trim('.', '\\');
            return Path.GetFullPath(Path.Combine(parent, path, file));
        }
    }
}