using System.Collections.Generic;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;

namespace Calamari
{
    public class DeferredLogger : AbstractLog
    {
        readonly ILog log;
        readonly List<LogAction> deferredActions = new List<LogAction>();
        bool releaseDeferredLogs;

        public DeferredLogger(ILog log)
        {
            this.log = log;
        }
        
        protected override void StdOut(string message)
        {
            if (releaseDeferredLogs)
                log.Info(message);
        }

        protected override void StdErr(string message)
        {
            if (releaseDeferredLogs)
                log.Error(message);
        }

        public override void Verbose(string message)
        {
            if (releaseDeferredLogs)
                log.Verbose(message);
            else
                deferredActions.Add(new LogAction { Type = LogActionType.Verbose, Message = message });
        }

        public override void Info(string message)
        {
            if (releaseDeferredLogs)
                log.Info(message);
            else
                deferredActions.Add(new LogAction { Type = LogActionType.Info, Message = message });
        }

        public override void Warn(string message)
        {
            if (releaseDeferredLogs)
                log.Warn(message);
            else
                deferredActions.Add(new LogAction { Type = LogActionType.Warn, Message = message });
        }

        public override void Error(string message)
        {
            if (releaseDeferredLogs)
                log.Error(message);
            else
                deferredActions.Add(new LogAction { Type = LogActionType.Error, Message = message });
        }

        public override void WriteServiceMessage(ServiceMessage serviceMessage)
        {
            if (releaseDeferredLogs)
                log.WriteServiceMessage(serviceMessage);
            else
                deferredActions.Add(new LogAction { Type = LogActionType.ServiceMessage, ServiceMessage = serviceMessage });
        }

        public void FlushDeferredLogs()
        {
            if (!releaseDeferredLogs)
            {
                releaseDeferredLogs = true;
                
                foreach (var action in deferredActions)
                {
                    switch (action.Type)
                    {
                        case LogActionType.Verbose:
                            log.Verbose(action.Message);
                            break;
                        case LogActionType.Info:
                            log.Info(action.Message);
                            break;
                        case LogActionType.Warn:
                            log.Warn(action.Message);
                            break;
                        case LogActionType.Error:
                            log.Error(action.Message);
                            break;
                        case LogActionType.ServiceMessage:
                            log.WriteServiceMessage(action.ServiceMessage);
                            break;
                    }
                }
                
                deferredActions.Clear();
            }
        }

        private class LogAction
        {
            public LogActionType Type { get; set; }
            public string Message { get; set; }
            public ServiceMessage ServiceMessage { get; set; }
        }

        private enum LogActionType
        {
            Verbose,
            Info,
            Warn,
            Error,
            ServiceMessage
        }
    }
}
