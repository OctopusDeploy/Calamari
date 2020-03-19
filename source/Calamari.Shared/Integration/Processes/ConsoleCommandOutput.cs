namespace Calamari.Integration.Processes
{
    public class ConsoleCommandOutput : ICommandOutput
    {
        readonly bool outputAsVerbose;

        public ConsoleCommandOutput(bool outputAsVerbose)
        {
            this.outputAsVerbose = outputAsVerbose;
        }

        public void WriteInfo(string line)
        {
            if(outputAsVerbose)
                Log.Verbose(line);
            else
                Log.Info(line);
        }

        public void WriteError(string line)
        {
            Log.Error(line);
        }
    }
}