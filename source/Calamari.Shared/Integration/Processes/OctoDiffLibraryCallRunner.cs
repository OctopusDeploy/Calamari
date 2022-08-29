using Calamari.Common.Features.Processes;

namespace Calamari.Integration.Processes
{
    public class OctoDiffLibraryCallRunner
    {
        public CommandLine OctoDiff { get; }

        public OctoDiffLibraryCallRunner()
        {
            OctoDiff = new CommandLine(Octodiff.Program.Main);
        }

        public CommandResult Execute()
        {
            var runner = new LibraryCallRunner();
            var result = runner.Execute(OctoDiff.BuildLibraryCall());
            return result;
        }
    }
}