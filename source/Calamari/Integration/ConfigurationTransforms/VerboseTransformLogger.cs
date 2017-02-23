using System;
#if USE_OCTOPUS_XMLT
using Octopus.Web.XmlTransform;
#else
using Microsoft.Web.XmlTransform;
#endif

namespace Calamari.Integration.ConfigurationTransforms
{
    public class VerboseTransformLogger : IXmlTransformationLogger
    {
        public event LogDelegate Warning;
        readonly TransformLoggingOptions transformLoggingOptions;
        readonly ILog log;

        public VerboseTransformLogger(TransformLoggingOptions transformLoggingOptions, ILog log)
        {
            this.transformLoggingOptions = transformLoggingOptions;
            this.log = log;
        }

        public void LogMessage(string message, params object[] messageArgs)
        {
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.DoNotLogVerbose))
            {
                return;
            }

            log.VerboseFormat(message, messageArgs);
        }

        public void LogMessage(MessageType type, string message, params object[] messageArgs)
        {
            LogMessage(message, messageArgs);
        }

        public void LogWarning(string message, params object[] messageArgs)
        {
            Warning?.Invoke(this, new WarningDelegateArgs(string.Format(message, messageArgs)));
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.LogWarningsAsInfo))
            {
                log.InfoFormat(message, messageArgs);
            }
            else
            {
                log.WarnFormat(message, messageArgs);
            }
        }

        public void LogWarning(string file, string message, params object[] messageArgs)
        {
            Warning?.Invoke(this, new WarningDelegateArgs($"{file}: {string.Format(message, messageArgs)}"));
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.LogWarningsAsInfo))
            {
                log.InfoFormat("File {0}: ", file);
                log.InfoFormat(message, messageArgs);
            }
            else
            {
                log.WarnFormat("File {0}: ", file);
                log.WarnFormat(message, messageArgs);
            }
        }

        public void LogWarning(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
        {
            Warning?.Invoke(this, new WarningDelegateArgs($"{file}({lineNumber},{linePosition}): {string.Format(message, messageArgs)}"));
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.LogWarningsAsInfo))
            {
                log.InfoFormat("File {0}, line {1}, position {2}: ", file, lineNumber, linePosition);
                log.InfoFormat(message, messageArgs);
            }
            else
            {
                log.WarnFormat("File {0}, line {1}, position {2}: ", file, lineNumber, linePosition);
                log.WarnFormat(message, messageArgs);
            }
        }

        public void LogError(string message, params object[] messageArgs)
        {
            log.ErrorFormat(message, messageArgs);
        }

        public void LogError(string file, string message, params object[] messageArgs)
        {
            log.ErrorFormat("File {0}: ", file);
            log.ErrorFormat(message, messageArgs);
        }

        public void LogError(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
        {
            log.ErrorFormat("File {0}, line {1}, position {2}: ", file, lineNumber, linePosition);
            log.ErrorFormat(message, messageArgs);
        }

        public void LogErrorFromException(Exception ex)
        {
            log.ErrorFormat(ex.ToString());
        }

        public void LogErrorFromException(Exception ex, string file)
        {
            log.ErrorFormat("File {0}: ", file);
            log.ErrorFormat(ex.ToString());
        }

        public void LogErrorFromException(Exception ex, string file, int lineNumber, int linePosition)
        {
            log.ErrorFormat("File {0}, line {1}, position {2}: ", file, lineNumber, linePosition);
            log.ErrorFormat(ex.ToString());
        }

        public void StartSection(string message, params object[] messageArgs)
        {
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.DoNotLogVerbose))
            {
                return;
            }

            log.VerboseFormat(message, messageArgs);
        }

        public void StartSection(MessageType type, string message, params object[] messageArgs)
        {
            StartSection(message, messageArgs);
        }

        public void EndSection(string message, params object[] messageArgs)
        {
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.DoNotLogVerbose))
            {
                return;
            }

            log.VerboseFormat(message, messageArgs);
        }

        public void EndSection(MessageType type, string message, params object[] messageArgs)
        {
            EndSection(message, messageArgs);
        }
    }
}