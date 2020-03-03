using System;
using System.Globalization;
using System.Text;
using Calamari.Integration.Processes;
using Calamari.Util;
using Octopus.Versioning;
using Octostache;

namespace Calamari
{
    public class LogWrapper : ILog
    {
        public void Verbose(string message)
        {
            Log.Verbose(message);
        }

        public void VerboseFormat(string message, params object[] args)
        {
            Log.VerboseFormat(message, args);
        }

        public void Info(string message)
        {
            Log.Info(message);
        }

        public void InfoFormat(string message, params object[] args)
        {
            Log.Info(message, args);
        }

        public void Warn(string message)
        {
            Log.Warn(message);
        }

        public void WarnFormat(string message, params object[] args)
        {
            Log.WarnFormat(message, args);
        }

        public void Error(string message)
        {
            Log.Error(message);
        }

        public void ErrorFormat(string message, params object[] args)
        {
            Log.ErrorFormat(message, args);
        }

    }

    public interface ILog
    {
        void Verbose(string message);
        void VerboseFormat(string message, params object[] args);
        void Info(string message);
        void InfoFormat(string message, params object[] args);
        void Warn(string message);
        void WarnFormat(string message, params object[] args);
        void Error(string message);
        void ErrorFormat(string message, params object[] args);
    }

    public class Log
    {
        static string stdOutMode;

        static readonly object Sync = new object();

        public static IndentedTextWriter StdOut;
        public static IndentedTextWriter StdErr;

        static Log()
        {
            SetWriters();
        }

        public static void SetWriters()
        {
            StdOut = new IndentedTextWriter(Console.Out, "  ");
            StdErr = new IndentedTextWriter(Console.Error, "  ");
        }


        static void SetMode(string mode)
        {
            if (stdOutMode == mode) return;
            StdOut.WriteLine("##octopus[stdout-" + mode + "]");
            stdOutMode = mode;
        }

        public static void Verbose(string message)
        {
            lock (Sync)
            {
                SetMode("verbose");
                StdOut.WriteLine(message);
            }
        }

        public static void SetOutputVariable(string name, string value, bool isSensitive = false)
        {
            SetOutputVariable(name, value, null, isSensitive);
        }

        public static void SetOutputVariable(string name, string value, IVariables variables, bool isSensitive = false)
        {
            Guard.NotNull(name, "name can not be null");
            Guard.NotNull(value, "value can not be null");

            Info(isSensitive
                ? $"##octopus[setVariable name=\"{ConvertServiceMessageValue(name)}\" value=\"{ConvertServiceMessageValue(value)}\" sensitive=\"{ConvertServiceMessageValue(Boolean.TrueString)}\"]"
                : $"##octopus[setVariable name=\"{ConvertServiceMessageValue(name)}\" value=\"{ConvertServiceMessageValue(value)}\"]");

            variables?.SetOutputVariable(name, value);
        }

        public static void NewOctopusArtifact(string fullPath, string name, long fileLength)
        {
            Info($"##octopus[createArtifact path=\"{ConvertServiceMessageValue(fullPath)}\" name=\"{ConvertServiceMessageValue(name)}\" length=\"{ConvertServiceMessageValue(fileLength.ToString())}\"]");
        }

        static string ConvertServiceMessageValue(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        public static void VerboseFormat(string messageFormat, params object[] args)
        {
            Verbose(string.Format(messageFormat, args));
        }

        public static void Info(string message)
        {
            lock (Sync)
            {
                SetMode("default");
                StdOut.WriteLine(message);
            }
        }

        public static void Info(string messageFormat, params object[] args)
        {
            Info(String.Format(messageFormat, args));
        }

        public static void Warn(string message)
        {
            lock (Sync)
            {
                SetMode("warning");
                StdOut.WriteLine(message);
            }
        }

        public static void WarnFormat(string messageFormat, params object[] args)
        {
            Warn(String.Format(messageFormat, args));
        }

        public static void Error(string message)
        {
            lock (Sync)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                StdErr.WriteLine(message);
                Console.ResetColor();
            }
        }

        public static void LogLink(string uri, string description = null)
        {
            Info(Link(uri, description));
        }

        public static string Link(string uri, string description = null)
        {
            return $"[{description ?? uri}]({uri})";
        }

        public static void ErrorFormat(string messageFormat, params object[] args)
        {
            Error(string.Format(messageFormat, args));
        }

        public static class ServiceMessages
        {
            public static string ConvertServiceMessageValue(string value)
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
            }

            public static void PackageFound(string packageId, IVersion packageVersion, string packageHash,
                string packageFileExtension, string packageFullPath, bool exactMatchExists = false)
            {
                if (exactMatchExists)
                    Verbose("##octopus[calamari-found-package]");

                VerboseFormat("##octopus[foundPackage id=\"{0}\" version=\"{1}\" versionFormat=\"{2}\" hash=\"{3}\" remotePath=\"{4}\" fileExtension=\"{5}\"]",
                    ConvertServiceMessageValue(packageId),
                    ConvertServiceMessageValue(packageVersion.ToString()),
                    ConvertServiceMessageValue(packageVersion.Format.ToString()),
                    ConvertServiceMessageValue(packageHash),
                    ConvertServiceMessageValue(packageFullPath),
                    ConvertServiceMessageValue(packageFileExtension));
            }

            public static void Progress(int percentage, string message)
            {
                VerboseFormat("##octopus[progress percentage=\"{0}\" message=\"{1}\"]",
                    ConvertServiceMessageValue(percentage.ToString(CultureInfo.InvariantCulture)),
                    ConvertServiceMessageValue(message));
            }

            public static void DeltaVerification(string remotePath, string hash, long size)
            {
                VerboseFormat("##octopus[deltaVerification remotePath=\"{0}\" hash=\"{1}\" size=\"{2}\"]",
                    ConvertServiceMessageValue(remotePath),
                    ConvertServiceMessageValue(hash),
                    ConvertServiceMessageValue(size.ToString(CultureInfo.InvariantCulture)));
            }

            public static void DeltaVerificationError(string error)
            {
                VerboseFormat("##octopus[deltaVerification error=\"{0}\"]", ConvertServiceMessageValue(error));
            }
        }
    }
}