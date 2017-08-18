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
            string workingDirectory = null)
            : base(FormatMessage(commandLine, exitCode, additionalInformation, workingDirectory))
        {
        }

        private static string FormatMessage(
            string commandLine, 
            int exitCode, 
            string additionalInformation,
            string workingDirectory)
        {
            var sb = new StringBuilder("The following command: ");
            sb.AppendLine(commandLine);
            
            if (!String.IsNullOrEmpty(workingDirectory))
            {
                sb.Append("With the working firectory of: ")
                    .AppendLine(workingDirectory);
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