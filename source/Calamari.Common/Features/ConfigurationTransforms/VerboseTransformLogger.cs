using System;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Web.XmlTransform;

namespace Calamari.Common.Features.ConfigurationTransforms
{
    public class VerboseTransformLogger : IXmlTransformationLogger
    {
        public event LogDelegate? Error;
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
            LogWarn(message, messageArgs);
        }

        public void LogWarning(string file, string message, params object[] messageArgs)
        {
            LogWarn("File {0}:", file);
            LogWarn(message, messageArgs);
        }

        public void LogWarning(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
        {
            LogWarn("File {0}, line {1}, position {2}: ", file, lineNumber, linePosition);
            LogWarning(message, messageArgs);
        }

        void LogWarn(string message, params object[] messageArgs)
        {
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.LogWarningsAsErrors))
            {
                log.ErrorFormat(message, messageArgs);
                Error?.Invoke(this);
            }
            else if (transformLoggingOptions.HasFlag(TransformLoggingOptions.LogWarningsAsInfo))
            {
                log.InfoFormat(message, messageArgs);
            }
            else
            {
                log.WarnFormat(message, messageArgs);
            }
        }

        public void LogError(string message, params object[] messageArgs)
        {
            LogErr(message, messageArgs);
        }

        public void LogError(string file, string message, params object[] messageArgs)
        {
            LogErr("File {0}: ", file);
            LogErr(message, messageArgs);
        }

        public void LogError(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
        {
            LogErr("File {0}, line {1}, position {2}:", file, lineNumber, linePosition);
            LogErr(message, messageArgs);
        }

        public void LogErrorFromException(Exception ex)
        {
            LogErr(ex.ToString());
        }

        public void LogErrorFromException(Exception ex, string file)
        {
            LogErr("File {0}:", file);
            LogErr(ex.ToString());
        }

        public void LogErrorFromException(Exception ex, string file, int lineNumber, int linePosition)
        {
            LogErr("File {0}, line {1}, position {2}:", file, lineNumber, linePosition);
            LogErr(ex.ToString());
        }

        void LogErr(string message, params object[] messageArgs)
        {
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.LogExceptionsAsWarnings))
            {
                log.WarnFormat(message, messageArgs);
            }
            else
            {
                log.ErrorFormat(message, messageArgs);
                Error?.Invoke(this);
            }
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