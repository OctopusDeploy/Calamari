namespace Calamari.Integration.Processes
{
    public interface ICommandOutput
    {
        void WriteInfo(string line);
        void WriteError(string line);
    }
}