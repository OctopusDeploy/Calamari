﻿using System;
using System.Linq;

namespace Calamari.Common.Features.Processes
{
    public class CommandResult
    {
        readonly string command;
        readonly string? workingDirectory;

        public CommandResult(string command, int exitCode, string? additionalErrors = null, string? workingDirectory = null)
            : this(command, exitCode, null, additionalErrors, workingDirectory)
        {
        }

        public CommandResult(string command, int exitCode, string? output, string? additionalErrors = null, string? workingDirectory = null)
        {
            this.command = command;
            ExitCode = exitCode;
            Errors = additionalErrors;
            this.workingDirectory = workingDirectory;
            this.Output = output;
        }

        public int ExitCode { get; }

        public string? Errors { get; }
        public string? Output { get; }

        public bool HasErrors => !string.IsNullOrWhiteSpace(Errors) && ErrorsExcludeServiceMessages(Errors);

        public void VerifySuccess()
        {
            if (ExitCode != 0)
                throw new CommandLineException(
                    command,
                    ExitCode,
                    Errors,
                    workingDirectory);
        }

        static bool ErrorsExcludeServiceMessages(string s) =>
            s.Split(new[] {Environment.NewLine}, StringSplitOptions.None)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Any(s => !s.StartsWith("##octopus"));
    }
}
