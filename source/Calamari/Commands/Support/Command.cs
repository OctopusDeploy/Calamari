using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Deployment;
using Octostache;

namespace Calamari.Commands.Support
{
    public abstract class Command : ICommand
    {
        protected Command()
        {
            Options = new OptionSet();
        }

        protected OptionSet Options { get; private set; }

        public void GetHelp(TextWriter writer)
        {
            Options.WriteOptionDescriptions(writer);
        }

        public abstract int Execute(string[] commandLineArguments);
    }
}
