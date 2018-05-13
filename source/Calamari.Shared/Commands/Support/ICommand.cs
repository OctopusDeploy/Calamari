using System.IO;

namespace Calamari.Commands.Support
{
    public interface ICommand
    {
        void GetHelp(TextWriter writer);
        int Execute(string[] commandLineArguments);
    }
}