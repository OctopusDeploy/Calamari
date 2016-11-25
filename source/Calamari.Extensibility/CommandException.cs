using System;

namespace Calamari.Extensibility
{
    public class CommandException : Exception
    {
        public int ExitCode { get; }

        public CommandException(string message)
            : this(message, 1)
        {
        }

        public CommandException(string message, int exitCode)
            : base(message)
        {
            ExitCode = exitCode;
        }
    }
}