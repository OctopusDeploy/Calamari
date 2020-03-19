namespace Calamari.Integration.Processes
{
    public class ConsoleCommandOutput : ICommandOutput
    {
        readonly ILog log;

        public ConsoleCommandOutput() : this(ConsoleLog.Instance)
        {
        }

        public ConsoleCommandOutput(ILog log)
        {
            this.log = log;
        }
        
        public void WriteInfo(string line)
        {
            log.Info(line);
        }

        public void WriteError(string line)
        {
            log.Error(line);
        }
    }
}