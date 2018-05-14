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

        public void GetHelp(TextWriter writer)
        {
            Options.WriteOptionDescriptions(writer);
        }

        public abstract int Execute(string[] commandLineArguments);
    }
}
