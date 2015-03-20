namespace Calamari.Integration.Processes
{
    public class CommandLineInvocation
    {
        readonly string executable;
        readonly string arguments;

        public CommandLineInvocation(string executable, string arguments)
        {
            this.executable = executable;
            this.arguments = arguments;
        }

        public string Executable
        {
            get { return executable; }
        }

        public string Arguments
        {
            get { return arguments; }
        }

        public override string ToString()
        {
            return "\"" + executable + "\" " + arguments;
        }
    }
}