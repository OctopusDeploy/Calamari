using System;

namespace Calamari.Common.Features.Processes
{
    public class CommandResult
    {
        readonly string command;
        readonly string? workingDirectory;
        readonly bool timedOut;

        public CommandResult(string command, int exitCode, string? additionalErrors = null, string? workingDirectory = null, bool timedOut = false)
        {
            this.command = command;
            ExitCode = exitCode;
            Errors = additionalErrors;
            this.workingDirectory = workingDirectory;
            this.timedOut = timedOut;
        }

        public int ExitCode { get; }

        public string? Errors { get; }

        public bool HasErrors => !string.IsNullOrWhiteSpace(Errors);

        public bool TimedOut => this.timedOut;

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