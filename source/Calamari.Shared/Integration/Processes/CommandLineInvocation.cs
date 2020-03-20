using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Calamari.Util;

namespace Calamari.Integration.Processes
{
    public class CommandLineInvocation
    {
        string workingDirectory;

        public CommandLineInvocation(string executable, params string[] arguments)
        {
            Executable = executable;
            Arguments = arguments.Where(a => !string.IsNullOrWhiteSpace(a)).Join(" ");
        }

        public string Executable { get; }

        public string Arguments { get; }

        public string UserName { get; set; }

        public SecureString Password { get; set; }
        
        public Dictionary<string, string> EnvironmentVars { get; set; }
        
        /// <summary>
        /// Prevent this execution from starting if another execution is running that also has this set to true.
        /// It does not isolate from executions that have this set to false. 
        /// </summary>
        public bool Isolate { get; set; }

        /// <summary>
        /// Whether to log the execution output to the Calamari Standard out (i.e. it will be send back to Octopus)
        /// </summary>
        public bool OutputToCalamariConsole { get; set; } = true;
        
        /// <summary>
        /// Start the logging as verbose. The executed command may change logging level itself via service messages.
        /// </summary>
        public bool OutputAsVerbose { get; set; }
        
        /// <summary>
        /// Add a non-standard output destination for the execution output 
        /// </summary>
        public ICommandOutput AdditionalOutput { get; set; } 

        /// <summary>
        /// The initial working-directory for the invocation.
        /// Defaults to Environment.CurrentDirectory
        /// </summary>
        public string WorkingDirectory
        {
            get => workingDirectory ?? Environment.CurrentDirectory;
            set => workingDirectory = value;
        }


        public override string ToString()
        {
            return "\"" + Executable + "\" " + Arguments;
        }
    }
}