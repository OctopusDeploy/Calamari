using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Plumbing.Logging
{
    public static class Log
    {
        public static void Verbose(string message)
        {
            ConsoleLog.Instance.Verbose(message);
        }

        public static void VerboseFormat(string message, params object[] args)
        {
            ConsoleLog.Instance.VerboseFormat(message, args);
        }

        public static void Info(string message)
        {
            ConsoleLog.Instance.Info(message);
        }

        public static void Info(string message, params object[] args)
        {
            ConsoleLog.Instance.InfoFormat(message, args);
        }

        public static void Warn(string message)
        {
            ConsoleLog.Instance.Warn(message);
        }

        public static void WarnFormat(string message, params object[] args)
        {
            ConsoleLog.Instance.WarnFormat(message, args);
        }

        public static void Error(string message)
        {
            ConsoleLog.Instance.Error(message);
        }

        public static void ErrorFormat(string message, params object[] args)
        {
            ConsoleLog.Instance.ErrorFormat(message, args);
        }

        public static void SetOutputVariable(string name, string value, IVariables variables, bool isSensitive = false)
        {
            ConsoleLog.Instance.SetOutputVariable(name, value, variables, isSensitive);
        }
    }

    public class ConsoleLog : AbstractLog
    {
        public static ConsoleLog Instance = new ConsoleLog();
        readonly IndentedTextWriter stdOut;
        readonly IndentedTextWriter stdErr;

        ConsoleLog()
        {
            stdOut = new IndentedTextWriter(Console.Out, "  ");
            stdErr = new IndentedTextWriter(Console.Error, "  ");
        }

        protected override void StdOut(string message)
        {
            stdOut.WriteLine(message);
        }

        protected override void StdErr(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            stdErr.WriteLine(message);
            Console.ResetColor();
        }
    }

    public abstract class AbstractLog : ILog
    {
        readonly object sync = new object();
        string? stdOutMode;
        readonly Dictionary<string, string> redactionMap = new Dictionary<string, string>();

        protected abstract void StdOut(string message);
        protected abstract void StdErr(string message);

        protected string ProcessRedactions(string message)
        {
            return redactionMap.Aggregate(message, (current, pair) => current.Replace(pair.Key, pair.Value));
        }

        void SetMode(string mode)
        {
            if (stdOutMode == mode)
                return;
            StdOut("##octopus[stdout-" + mode + "]");
            stdOutMode = mode;
        }

        public void AddValueToRedact(string value, string replacement)
        {
            lock (sync)
            {
                redactionMap[value] = replacement;
            }
        }

        public virtual void Verbose(string message)
        {
            lock (sync)
            {
                SetMode("verbose");
                StdOut(ProcessRedactions(message));
            }
        }

        public virtual void VerboseFormat(string messageFormat, params object[] args)
        {
            Verbose(string.Format(messageFormat, args));
        }

        public virtual void Info(string message)
        {
            lock (sync)
            {
                SetMode("default");
                StdOut(ProcessRedactions(message));
            }
        }

        public virtual void InfoFormat(string messageFormat, params object[] args)
        {
            Info(string.Format(messageFormat, args));
        }

        public virtual void Warn(string message)
        {
            lock (sync)
            {
                SetMode("warning");
                StdOut(ProcessRedactions(message));
            }
        }

        public virtual void WarnFormat(string messageFormat, params object[] args)
        {
            Warn(string.Format(messageFormat, args));
        }

        public virtual void Error(string message)
        {
            lock (sync)
            {
                StdErr(ProcessRedactions(message));
            }
        }

        public virtual void ErrorFormat(string messageFormat, params object[] args)
        {
            Error(string.Format(messageFormat, args));
        }

        public void SetOutputVariableButDoNotAddToVariables(string name, string value, bool isSensitive = false)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Info(isSensitive
                ? $"##octopus[setVariable name=\"{ConvertServiceMessageValue(name)}\" value=\"{ConvertServiceMessageValue(value)}\" sensitive=\"{ConvertServiceMessageValue(bool.TrueString)}\"]"
                : $"##octopus[setVariable name=\"{ConvertServiceMessageValue(name)}\" value=\"{ConvertServiceMessageValue(value)}\"]");
        }

        public void SetOutputVariable(string name, string value, IVariables variables, bool isSensitive = false)
        {
            SetOutputVariableButDoNotAddToVariables(name, value, isSensitive);
            variables?.SetOutputVariable(name, value);
        }

        public void NewOctopusArtifact(string fullPath, string name, long fileLength)
        {
            Info($"##octopus[createArtifact path=\"{ConvertServiceMessageValue(fullPath)}\" name=\"{ConvertServiceMessageValue(name)}\" length=\"{ConvertServiceMessageValue(fileLength.ToString())}\"]");
        }

        public virtual void WriteServiceMessage(ServiceMessage serviceMessage)
        {
            Info(serviceMessage.ToString());
        }

        public void Progress(int percentage, string message)
        {
            VerboseFormat("##octopus[progress percentage=\"{0}\" message=\"{1}\"]",
                ConvertServiceMessageValue(percentage.ToString(CultureInfo.InvariantCulture)),
                ConvertServiceMessageValue(message));
        }

        public void DeltaVerification(string remotePath, string hash, long size)
        {
            VerboseFormat("##octopus[deltaVerification remotePath=\"{0}\" hash=\"{1}\" size=\"{2}\"]",
                ConvertServiceMessageValue(remotePath),
                ConvertServiceMessageValue(hash),
                ConvertServiceMessageValue(size.ToString(CultureInfo.InvariantCulture)));
        }

        public void DeltaVerificationError(string error)
        {
            VerboseFormat("##octopus[deltaVerification error=\"{0}\"]", ConvertServiceMessageValue(error));
        }

        public string FormatLink(string uri, string? description = null)
        {
            return $"[{description ?? uri}]({uri})";
        }

        public static string ConvertServiceMessageValue(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }
    }
}