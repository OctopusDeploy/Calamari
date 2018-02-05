using System;
using System.Collections.Specialized;
using System.Security;
using Calamari.Util;

namespace Calamari.Integration.Processes
{
    public class CommandLineInvocation
    {
        readonly string workingDirectory;

        public CommandLineInvocation(string executable, string arguments, StringDictionary environmentVars = null)
        {
            Executable = executable;
            Arguments = arguments;
            EnvironmentVars = environmentVars;
        }

        public CommandLineInvocation(
            string executable, 
            string arguments, 
            string workingDirectory, 
            StringDictionary environmentVars = null, 
            string userName = null, 
            SecureString password = null)
            : this(executable, arguments)
        {
            this.workingDirectory = workingDirectory;
            UserName = userName;
            Password = password;
        }

        public string Executable { get; }

        public string Arguments { get; }

        public string UserName { get; }

        public SecureString Password { get; }
        
        public StringDictionary EnvironmentVars { get; }

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