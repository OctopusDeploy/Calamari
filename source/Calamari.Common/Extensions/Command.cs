using System;
using System.IO;

namespace Calamari.Commands.Support
{
    public abstract class Command : ICommand
    {
        protected Command()
        {
            Options = new OptionSet();
        }

        protected OptionSet Options { get; private set; }

        public virtual int Execute(string[] commandLineArguments)
        {
            return Execute();
        }
        
        public virtual int Execute()
        {
            throw new NotImplementedException(
                "Command Line Arguments are deprecated. Should be overriding Execute with no Parameters method");
        }
    }
}
