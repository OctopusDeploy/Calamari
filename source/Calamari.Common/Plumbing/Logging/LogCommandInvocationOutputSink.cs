using System;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Common.Plumbing.Logging
{
    public class LogCommandInvocationOutputSink : ICommandInvocationOutputSink
    {
        readonly ILog log;
        readonly bool outputAsVerbose;

        public LogCommandInvocationOutputSink(ILog log, bool outputAsVerbose)
        {
            this.log = log;
            this.outputAsVerbose = outputAsVerbose;
        }

        public void WriteInfo(string line)
        {
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