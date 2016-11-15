namespace Calamari.Integration.Processes
{
    public class OctoDiffLibraryCallRunner
    {
        public CommandLine OctoDiff { get; }

        public OctoDiffLibraryCallRunner()
        {
            OctoDiff = CommandLine.Execute(Octodiff.Program.Main);
        }

        public CommandResult Execute()
        {
            var runner = new LibraryCallRunner();
            var result = runner.Execute(OctoDiff.BuildLibraryCall());
            return result;
        }
    }
}