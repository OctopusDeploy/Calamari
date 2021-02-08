namespace Calamari.Tools
{
    public interface IExecuteTool
    {
        int Execute(string command, string inputs, string[] commandLineArguments);
    }
}