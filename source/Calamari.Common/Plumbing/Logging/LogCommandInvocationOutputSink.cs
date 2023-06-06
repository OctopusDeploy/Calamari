using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.ServiceMessages;

namespace Calamari.Common.Plumbing.Logging
{
    public class LogCommandInvocationOutputSink : ICommandInvocationOutputSink
    {
        readonly ILog log;
        readonly bool outputAsVerbose;
        private readonly ServiceMessageParser serviceMessageParser;

        public LogCommandInvocationOutputSink(ILog log, bool outputAsVerbose)
        {
            this.log = log;
            this.outputAsVerbose = outputAsVerbose;
            serviceMessageParser = new ServiceMessageParser(ProcessServiceMessage);
        }

        private void ProcessServiceMessage(ServiceMessage serviceMessage)
        {
            log.WriteServiceMessage(serviceMessage);
        }

        public void WriteInfo(string line)
        {
            if (serviceMessageParser.Parse(line))
                return;

            if (outputAsVerbose)
                log.Verbose(line);
            else
                log.Info(line);
        }

        public void WriteError(string line)
        {
            log.Error(line);
        }
    }
}