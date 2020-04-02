using System.IO;

namespace Calamari.Commands.Support
{
    public abstract class Command : ICommand
    {
        protected Command()
        {
            Options = new OptionSet();
        }

        public IOptionSet Options { get; set; }
        public abstract int Execute(string[] commandLineArguments);
    }
}
