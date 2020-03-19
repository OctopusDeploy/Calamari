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
        
        public bool Isolate { get; set; }

        public bool OutputToCalamariConsole { get; set; } = true;
        
        public bool OutputAsVerbose { get; set; }
        
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