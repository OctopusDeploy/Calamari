using System;
using System.Text;

namespace Calamari.Common.Features.Processes
{
    public class CommandLineException : Exception
    {
        public CommandLineException(
            string commandLine,
            int exitCode,
            string? additionalInformation,
            string? workingDirectory = null)
            : base(FormatMessage(commandLine, exitCode, additionalInformation, workingDirectory))
        {
        }

        static string FormatMessage(
            string commandLine,
            int exitCode,
            string? additionalInformation,
            string? workingDirectory)
        {
            var sb = new StringBuilder("The following command: ");
            sb.AppendLine(commandLine);

            if (!string.IsNullOrEmpty(workingDirectory))
                sb.Append("With the working directory of: ")
                    .AppendLine(workingDirectory);

            sb.Append("Failed with exit code: ").Append(exitCode).AppendLine();
            if (!string.IsNullOrWhiteSpace(additionalInformation))
                sb.AppendLine(additionalInformation);
            return sb.ToString();
        }
    }
}