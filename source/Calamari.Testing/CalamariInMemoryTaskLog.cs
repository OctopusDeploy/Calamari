using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Diagnostics;

namespace Calamari.Testing
{
    public interface ITestTaskLog
    {
    }

    public class CalamariInMemoryTaskLog : ITestTaskLog
    {
        readonly StringBuilder log = new StringBuilder();

        public void Dispose()
        {
        }

        public bool IsVerboseEnabled { get; }
        public bool IsErrorEnabled { get; }
        public bool IsFatalEnabled { get; }
        public bool IsInfoEnabled { get; }
        public bool IsTraceEnabled { get; }
        public bool IsWarnEnabled { get; }

        public override string ToString()
        {
            return log.ToString();
        }

        public List<(string?, Exception?)> ErrorLog { get; } = new List<(string?, Exception?)>();
        public List<(string?, Exception?)> WarnLog { get; } = new List<(string?, Exception?)>();
        public List<(string?, Exception?)> InfoLog { get; } = new List<(string?, Exception?)>();
        public List<(string?, Exception?)> FatalLog { get; } = new List<(string?, Exception?)>();
        public List<(string?, Exception?)> TraceLog { get; } = new List<(string?, Exception?)>();
        public List<(string?, Exception?)> VerboseLog { get; } = new List<(string?, Exception?)>();

        public void WithSensitiveValues(string[] sensitiveValues)
        {
        }

        public void WithSensitiveValue(string sensitiveValue)
        {
        }

        public void Trace(string messageText)
        {
            Trace(null, messageText);
        }

        public void Trace(Exception error)
        {
            Trace(error, null);
        }

        public void Trace(Exception? error, string? messageText)
        {
            Write(TraceLog, messageText, error);
        }

        public void Verbose(string messageText)
        {
            Verbose(null, messageText);
        }

        public void Verbose(Exception error)
        {
            Verbose(error, null);
        }

        public void Verbose(Exception? error, string? messageText)
        {
            Write(VerboseLog, messageText, error);
        }

        public void Info(string messageText)
        {
            Info(null, messageText);
        }

        public void Info(Exception error)
        {
            Info(error, null);
        }

        public void Info(Exception? error, string? messageText)
        {
            Write(InfoLog, messageText, error);
        }

        public void Warn(string messageText)
        {
            Warn(null, messageText);
        }

        public void Warn(Exception error)
        {
            Warn(error, null);
        }

        public void Warn(Exception? error, string? messageText)
        {
            Write(WarnLog, messageText, error);
        }

        public void Error(string messageText)
        {
            Error(null, messageText);
        }

        public void Error(Exception error)
        {
            Error(error, null);
        }

        public void Error(Exception? error, string? messageText)
        {
            Write(ErrorLog, messageText, error);
        }

        public void Fatal(string messageText)
        {
            Fatal(null, messageText);
        }

        public void Fatal(Exception error)
        {
            Fatal(error, null);
        }

        public void Fatal(Exception? error, string? messageText)
        {
            Write(FatalLog, messageText, error);
        }

        public void Write(LogCategory category, string messageText)
        {
            Write(category, null, messageText);
        }

        public void Write(LogCategory category, Exception error)
        {
            Write(category, error, null);
        }

        public void Write(LogCategory category, Exception? error, string? messageText)
        {
            switch (category)
            {
                case LogCategory.Trace:
                    Trace(error, messageText);
                    break;
                case LogCategory.Verbose:
                    Verbose(error, messageText);
                    break;
                case LogCategory.Info:
                    Info(error, messageText);
                    break;
                case LogCategory.Planned:
                    break;
                case LogCategory.Highlight:
                    break;
                case LogCategory.Abandoned:
                    break;
                case LogCategory.Wait:
                    break;
                case LogCategory.Progress:
                    break;
                case LogCategory.Finished:
                    break;
                case LogCategory.Warning:
                    Warn(error, messageText);
                    break;
                case LogCategory.Error:
                    Error(error, messageText);
                    break;
                case LogCategory.Fatal:
                    Fatal(error, messageText);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
        }

        public void WriteFormat(LogCategory category, string messageFormat, params object[] args)
        {
            WriteFormat(category, null, messageFormat, args);
        }

        public void WriteFormat(LogCategory category, Exception? error, string messageFormat, params object[] args)
        {
            Write(category, error, String.Format(messageFormat, args));
        }

        public void TraceFormat(string messageFormat, params object[] args)
        {
            TraceFormat(null, messageFormat, args);
        }

        public void TraceFormat(Exception? error, string format, params object[] args)
        {
            Trace(error, String.Format(format, args));
        }

        public void VerboseFormat(string messageFormat, params object[] args)
        {
            VerboseFormat(null, messageFormat, args);
        }

        public void VerboseFormat(Exception? error, string format, params object[] args)
        {
            Verbose(error, String.Format(format, args));
        }

        public void InfoFormat(string messageFormat, params object[] args)
        {
            InfoFormat(null, messageFormat, args);
        }

        public void InfoFormat(Exception? error, string format, params object[] args)
        {
            Info(error, String.Format(format, args));
        }

        public void WarnFormat(string messageFormat, params object[] args)
        {
            WarnFormat(null, messageFormat, args);
        }

        public void WarnFormat(Exception? error, string format, params object[] args)
        {
            Warn(error, String.Format(format, args));
        }

        public void ErrorFormat(string messageFormat, params object[] args)
        {
            ErrorFormat(null, messageFormat, args);
        }

        public void ErrorFormat(Exception? error, string format, params object[] args)
        {
            Error(error, String.Format(format, args));
        }

        public void FatalFormat(string messageFormat, params object[] args)
        {
            FatalFormat(null, messageFormat, args);
        }

        public void FatalFormat(Exception? error, string format, params object[] args)
        {
            Fatal(error, String.Format(format, args));
        }

        public void Flush()
        {
        }

        public ITestTaskLog CreateBlock(string messageText)
        {
            return new CalamariInMemoryTaskLog();
        }

        public ITestTaskLog CreateBlock(string messageFormat, params object[] args)
        {
            return new CalamariInMemoryTaskLog();
        }

        public ITestTaskLog ChildContext(string[] sensitiveValues)
        {
            return new CalamariInMemoryTaskLog();
        }

        public ITestTaskLog PlanGroupedBlock(string messageText)
        {
            return new CalamariInMemoryTaskLog();
        }

        public ITestTaskLog PlanFutureBlock(string messageText)
        {
            return new CalamariInMemoryTaskLog();
        }

        public ITestTaskLog PlanFutureBlock(string messageFormat, params object[] args)
        {
            return new CalamariInMemoryTaskLog();
        }

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
        {
            return true;
        }

        public void UpdateProgress(int progressPercentage, string messageText)
        {
        }

        public void UpdateProgressFormat(int progressPercentage, string messageFormat, params object[] args)
        {
        }

        void Write(ICollection<(string?, Exception?)> list, string? message, Exception? error)
        {
            log.AppendLine(message);
            list.Add((message, error));
        }
    }
}