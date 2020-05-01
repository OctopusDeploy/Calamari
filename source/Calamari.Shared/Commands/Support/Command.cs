using System;
using System.IO;

namespace Calamari.Commands.Support
{
    public abstract class Command : ICommandWithArgs
    {
        protected Command()
        {
            Options = new OptionSet();
        }

        protected OptionSet Options { get; private set; }

        public abstract int Execute(string[] commandLineArguments);

    }
}
