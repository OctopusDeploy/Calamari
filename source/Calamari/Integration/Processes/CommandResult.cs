namespace Calamari.Integration.Processes
{
    public class CommandResult
    {
        private readonly string command;
        private readonly int exitCode;
        private readonly string additionalErrors;
        private readonly string workingDirectory;

        public CommandResult(string command, int exitCode) : this(command, exitCode, null)
        {
        }

        public CommandResult(string command, int exitCode, string additionalErrors)
            : this(command, exitCode, null, null)
        {
            
        }
        
        public CommandResult(string command, int exitCode, string additionalErrors, string workingDirectory)  
        {
            this.command = command;
            this.exitCode = exitCode;
            this.additionalErrors = additionalErrors;
            this.workingDirectory = workingDirectory;
        }

        public int ExitCode => exitCode;

        public string Errors => additionalErrors;

        public bool HasErrors => !string.IsNullOrWhiteSpace(additionalErrors);

        public CommandResult VerifySuccess()
        {
            if (exitCode != 0)
            {
                throw new CommandLineException(
                    command, 
                    exitCode, 
                    additionalErrors, 
                    workingDirectory);
            }

            return this;
        }
    }
}