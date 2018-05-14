namespace Calamari.Integration.Processes
{
    public class ConsoleCommandOutput : ICommandOutput
    {
        public void WriteInfo(string line)
        {
            Log.Info(line);
        }

        public void WriteError(string line)
        {
            Log.Error(line);
        }
    }
}