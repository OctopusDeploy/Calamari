using System;
using System.Text;

namespace Calamari.Integration.Processes
{
    public class CommandLineException : Exception
    {
        public CommandLineException(string commandLine, int exitCode, string additionalInformation)
            : base(FormatMessage(commandLine, exitCode, additionalInformation))
        {
        }

        private static string FormatMessage(string commandLine, int exitCode, string additionalInformation)
        {
            var sb = new StringBuilder("The following command:");
            sb.AppendLine(commandLine);
            sb.Append("Failed with exit code: ").Append(exitCode).AppendLine();
            if (!string.IsNullOrWhiteSpace(additionalInformation))
            {
                sb.AppendLine(additionalInformation);
            }
            return sb.ToString();
        }
    }
}