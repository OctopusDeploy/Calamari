using System.IO;

namespace Calamari.Commands.Support
{
    public interface ICommand
    {
        int Execute(string[] commandLineArguments);
    }
}