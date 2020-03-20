namespace Calamari.Integration.Processes
{
    public interface ICommandInvocationOutputSink
    {
        void WriteInfo(string line);
        void WriteError(string line);
    }
}