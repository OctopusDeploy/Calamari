using System;

namespace Calamari.Integration.Processes
{
    public class CommandLineInvocation
    {
        readonly string executable;
        readonly string arguments;
        readonly string workingDirectory; 

        public CommandLineInvocation(string executable, string arguments)
        {
            this.executable = executable;
            this.arguments = arguments;
        }

        public CommandLineInvocation(string executable, string arguments, string workingDirectory) 
            : this(executable, arguments)
        {
            this.workingDirectory = workingDirectory;
        }

        public string Executable
        {
            get { return executable; }
        }

        public string Arguments
        {
            get { return arguments; }
        }

        /// <summary>
        /// The initial working-directory for the invocation.
        /// Defaults to Environment.CurrentDirectory
        /// </summary>
        public string WorkingDirectory
        {
            get { return workingDirectory ?? Environment.CurrentDirectory;}
        }

        public override string ToString()
        {
            return "\"" + executable + "\" " + arguments;
        }
    }
}