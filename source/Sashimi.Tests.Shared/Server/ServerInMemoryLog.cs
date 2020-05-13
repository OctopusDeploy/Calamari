using System;
using Octopus.Diagnostics;

namespace Sashimi.Tests.Shared.Server
{
    public class ServerInMemoryLog : ILogWithContext
    {
        public ServerInMemoryLog()
        {
        }

        public void Trace(string messageText)
        {
           
        }

        public void Trace(Exception error)
        {
           
        }

        public void Trace(Exception error, string messageText)
        {
           
        }

        public void Verbose(string messageText)
        {
           
        }

        public void Verbose(Exception error)
        {
           
        }

        public void Verbose(Exception error, string messageText)
        {
           
        }

        public void Info(string messageText)
        {
           
        }

        public void Info(Exception error)
        {
           
        }

        public void Info(Exception error, string messageText)
        {
           
        }

        public void Warn(string messageText)
        {
           
        }

        public void Warn(Exception error)
        {
           
        }

        public void Warn(Exception error, string messageText)
        {
           
        }

        public void Error(string messageText)
        {
           
        }

        public void Error(Exception error)
        {
           
        }

        public void Error(Exception error, string messageText)
        {
           
        }

        public void Fatal(string messageText)
        {
           
        }

        public void Fatal(Exception error)
        {
           
        }

        public void Fatal(Exception error, string messageText)
        {
           
        }

        public void Write(LogCategory category, string messageText)
        {
           
        }

        public void Write(LogCategory category, Exception error)
        {
           
        }

        public void Write(LogCategory category, Exception error, string messageText)
        {
           
        }

        public void WriteFormat(LogCategory category, string messageFormat, params object[] args)
        {
           
        }

        public void WriteFormat(LogCategory category, Exception error, string messageFormat, params object[] args)
        {
           
        }

        public void TraceFormat(string messageFormat, params object[] args)
        {
           
        }

        public void TraceFormat(Exception error, string format, params object[] args)
        {
           
        }

        public void VerboseFormat(string messageFormat, params object[] args)
        {
           
        }

        public void VerboseFormat(Exception error, string format, params object[] args)
        {
           
        }

        public void InfoFormat(string messageFormat, params object[] args)
        {
           
        }

        public void InfoFormat(Exception error, string format, params object[] args)
        {
           
        }

        public void WarnFormat(string messageFormat, params object[] args)
        {
           
        }

        public void WarnFormat(Exception error, string format, params object[] args)
        {
           
        }

        public void ErrorFormat(string messageFormat, params object[] args)
        {
           
        }

        public void ErrorFormat(Exception error, string format, params object[] args)
        {
           
        }

        public void FatalFormat(string messageFormat, params object[] args)
        {
           
        }

        public void FatalFormat(Exception error, string format, params object[] args)
        {
           
        }

        public void Flush()
        {
           
        }

        public IDisposable OpenBlock(string messageText)
            => throw new NotImplementedException();

        public IDisposable OpenBlock(string messageFormat, params object[] args)
            => throw new NotImplementedException();

        public ILogContext PlanGroupedBlock(string messageText)
            => throw new NotImplementedException();

        public ILogContext PlanFutureBlock(string messageText)
            => throw new NotImplementedException();

        public ILogContext PlanFutureBlock(string messageFormat, params object[] args)
            => throw new NotImplementedException();

        public IDisposable WithinBlock(ILogContext logContext)
            => throw new NotImplementedException();

        public void Abandon()
        {
           
        }

        public void Reinstate()
        {
           
        }

        public void Finish()
        {
           
        }

        public bool IsEnabled(LogCategory category)
            => true;

        public void UpdateProgress(int progressPercentage, string messageText)
        {
           
        }

        public void UpdateProgressFormat(int progressPercentage, string messageFormat, params object[] args)
        {
           
        }

        public ILogContext? CurrentContext { get; }
        public bool IsVerboseEnabled { get; }
        public bool IsErrorEnabled { get; }
        public bool IsFatalEnabled { get; }
        public bool IsInfoEnabled { get; }
        public bool IsTraceEnabled { get; }
        public bool IsWarnEnabled { get; }
    }
}