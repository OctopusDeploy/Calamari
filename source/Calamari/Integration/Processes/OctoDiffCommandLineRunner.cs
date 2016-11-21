using System.IO;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Integration.ServiceMessages;
using Calamari.Util;
using System.Reflection;

namespace Calamari.Integration.Processes
{
    public class OctoDiffCommandLineRunner 
    {
        public CommandLine OctoDiff { get; }

        public OctoDiffCommandLineRunner()
        {
            OctoDiff = CommandLine.Execute(FindOctoDiffExecutable());
        }

        public CommandResult Execute()
        {
            var runner = new CommandLineRunner(new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(new CalamariVariableDictionary())));
            var result = runner.Execute(OctoDiff.Build());
            return result;
        }

        public static string FindOctoDiffExecutable()
        {
            var basePath = Path.GetDirectoryName(typeof(ApplyDeltaCommand).GetTypeInfo().Assembly.Location);
            var executable = Path.GetFullPath(Path.Combine(basePath, "Octodiff.exe"));
            if (File.Exists(executable))
                return executable;

            throw new CommandException(string.Format("Unable to find Octodiff.exe at {0}.", executable));
        }
    }
}