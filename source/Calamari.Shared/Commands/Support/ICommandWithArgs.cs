namespace Calamari.Commands.Support
{
    public interface ICommandWithArgs
    {
        int Execute(string[] commandLineArguments);
    }
}