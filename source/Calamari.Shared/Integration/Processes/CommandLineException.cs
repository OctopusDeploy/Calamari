using System;
using System.Text;

namespace Calamari.Integration.Processes
{
    public class CommandLineException : Exception
    {
        public CommandLineException(
            string commandLine,
            int exitCode,
            string additionalInformation,
            string workingDirectory = null,
            bool timedOut = false)
            : base(FormatMessage(commandLine, exitCode, additionalInformation, workingDirectory, timedOut))
        {
        }

        private static string FormatMessage(
            string commandLine, 
            int exitCode, 
            string additionalInformation,
            string workingDirectory,
            bool timedOut)
        {
            var sb = new StringBuilder("The following command: ");
            sb.AppendLine(commandLine);
            
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                sb.Append("With the working directory of: ")
                    .AppendLine(workingDirectory);
            }

            if (timedOut)
            {
                sb.Append("Timed out before execution completed. Check the Octopus.Action.Script.Timeout variable.").AppendLine();
            }

            sb.Append("Failed with exit code: ").Append(exitCode).AppendLine();
            if (!string.IsNullOrWhiteSpace(additionalInformation))
            {
                sb.AppendLine(additionalInformation);
            }
            return sb.ToString();
        }
    }
}