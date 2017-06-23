using System.IO;
using System.Linq;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Integration.ServiceMessages;
using Calamari.Util;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Calamari.Integration.Processes
{
    public class OctoDiffCommandLineRunner 
    {
        public CommandLine OctoDiff { get; }

        public OctoDiffCommandLineRunner()
        {
            OctoDiff = new CommandLine(FindOctoDiffExecutable());
        }

        public CommandResult Execute()
        {
            var runner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(new CalamariVariableDictionary())));
            var result = runner.Execute(OctoDiff.Build());
            return result;
        }

        public static string FindOctoDiffExecutable([CallerFilePath] string callerPath = null)
        {
            var basePath = Path.GetDirectoryName(typeof(ApplyDeltaCommand).Assembly.Location);
            var executable = Path.GetFullPath(Path.Combine(basePath, "Octodiff.exe"));
            if (File.Exists(executable))
                return executable;

            var alternatePath = Path.Combine(Path.GetDirectoryName(callerPath), @"..\bin"); // Resharper uses a weird path
            executable = Directory.EnumerateFiles(alternatePath, "Octodiff.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (executable != null)
                return executable;

            throw new CommandException($"Could not find Octodiff.exe at {executable} or {alternatePath}.");
        }
    }
}