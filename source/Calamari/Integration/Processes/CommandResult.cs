namespace Calamari.Integration.Processes
{
    public class CommandResult
    {
        private readonly string command;
        private readonly int exitCode;
        private readonly string additionalErrors;

        public CommandResult(string command, int exitCode) : this(command, exitCode, null)
        {
        }

        public CommandResult(string command, int exitCode, string additionalErrors)
        {
            this.command = command;
            this.exitCode = exitCode;
            this.additionalErrors = additionalErrors;
        }

        public int ExitCode
        {
            get { return exitCode; }
        }

        public string Errors
        {
            get { return additionalErrors; }
        }

        public CommandResult VerifySuccess()
        {
            if (exitCode != 0)
                throw new CommandLineException(command, exitCode, additionalErrors);

            return this;
        }
    }
}