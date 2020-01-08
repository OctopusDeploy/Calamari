using System;
using System.Collections.Generic;
using System.Security;
using System.Threading;

namespace Calamari.Integration.Processes
{
    public class CommandLineInvocation
    {
        readonly string workingDirectory;

        public CommandLineInvocation(string executable, string arguments, Dictionary<string, string> environmentVars = null, bool isolate = false, int timeoutMilliseconds = Timeout.Infinite)
        {
            Executable = executable;
            Arguments = arguments;
            EnvironmentVars = environmentVars;
            Isolate = isolate;
            TimeoutMilliseconds = timeoutMilliseconds;
        }

        public CommandLineInvocation(
            string executable, 
            string arguments, 
            string workingDirectory, 
            Dictionary<string, string> environmentVars = null, 
            string userName = null, 
            SecureString password = null,
            bool isolate = false,
            int timeoutMilliseconds = Timeout.Infinite)
            : this(executable, arguments, environmentVars, isolate)
        {
            this.workingDirectory = workingDirectory;
            UserName = userName;
            Password = password;
            TimeoutMilliseconds = timeoutMilliseconds;
        }

        public string Executable { get; }

        public string Arguments { get; }

        public string UserName { get; }

        public SecureString Password { get; }
        
        public Dictionary<string, string> EnvironmentVars { get; }
        public bool Isolate { get; }
        public int TimeoutMilliseconds { get; internal set; }

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