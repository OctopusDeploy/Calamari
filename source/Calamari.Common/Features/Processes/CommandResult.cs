using System;

namespace Calamari.Common.Features.Processes
{
    public class CommandResult
    {
        readonly string command;
        readonly string workingDirectory;

        public CommandResult(string command, int exitCode)
            : this(command, exitCode, null)
        {
        }

        public CommandResult(string command, int exitCode, string additionalErrors)
            : this(command, exitCode, null, null)
        {
        }

        public CommandResult(string command, int exitCode, string additionalErrors, string workingDirectory)
        {
            this.command = command;
            this.ExitCode = exitCode;
            this.Errors = additionalErrors;
            this.workingDirectory = workingDirectory;
        }

        public int ExitCode { get; }

        public string Errors { get; }

        public bool HasErrors => !string.IsNullOrWhiteSpace(Errors);

        public void VerifySuccess()
        {
            if (ExitCode != 0)
                throw new CommandLineException(
                    command,
                    ExitCode,
                    Errors,
                    workingDirectory);
        }
    }
}