using System;
using System.Collections.Generic;
using System.Security;

namespace Calamari.Integration.Processes
{
    public class CommandLineInvocation
    {
        readonly string workingDirectory;

        public CommandLineInvocation(string executable, string arguments, Dictionary<string, string> environmentVars = null)
        {
            Executable = executable;
            Arguments = arguments;
            EnvironmentVars = environmentVars;
        }

        public CommandLineInvocation(
            string executable, 
            string arguments, 
            string workingDirectory, 
            Dictionary<string, string> environmentVars = null, 
            string userName = null, 
            SecureString password = null)
            : this(executable, arguments, environmentVars)
        {
            this.workingDirectory = workingDirectory;
            UserName = userName;
            Password = password;
        }

        public string Executable { get; }

        public string Arguments { get; }

        public string UserName { get; }

        public SecureString Password { get; }
        
        public Dictionary<string, string> EnvironmentVars { get; }

        /// <summary>
        /// The initial working-directory for the invocation.
        /// Defaults to Environment.CurrentDirectory
        /// </summary>
        public string WorkingDirectory => workingDirectory ?? Environment.CurrentDirectory;

        public override string ToString()
        {
            return "\"" + Executable + "\" " + Arguments;
        }
    }
}