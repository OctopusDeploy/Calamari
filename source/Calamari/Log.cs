using System;
using System.Globalization;
using System.Text;
using Calamari.Integration.Processes;
using Calamari.Util;
using Octostache;

namespace Calamari
{
    public class LogWrapper : ILog, Calamari.Shared.ILog
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

        internal static IndentedTextWriter StdOut;
        internal static IndentedTextWriter StdErr;

        static Log()
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

        public static void SetOutputVariable(string name, string value)
        {
            SetOutputVariable(name, value, null);
        }

        public static void SetOutputVariable(string name, string value, VariableDictionary variables)
        {
            Info($"##octopus[setVariable name=\"{ConvertServiceMessageValue(name)}\" value=\"{ConvertServiceMessageValue(value)}\"]");

            variables?.SetOutputVariable(name, value);
        }

        static string ConvertServiceMessageValue(string value)
        {
            return Convert.ToBase64String(CrossPlatform.GetDefaultEncoding().GetBytes(value));
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

        public static void ErrorFormat(string messageFormat, params object[] args)
        {
            Error(string.Format(messageFormat, args));
        }

        public static class ServiceMessages
        {
            public static string ConvertServiceMessageValue(string value)
            {
                return Convert.ToBase64String(CrossPlatform.GetDefaultEncoding().GetBytes(value));
            }

            public static void PackageFound(string packageId, string packageVersion, string packageHash,
                string packageFileExtension, string packageFullPath, bool exactMatchExists = false)
            {
                if (exactMatchExists)
                    Verbose("##octopus[calamari-found-package]");

                VerboseFormat("##octopus[foundPackage id=\"{0}\" version=\"{1}\" hash=\"{2}\" remotePath=\"{3}\" fileExtension=\"{4}\"]",
                    ConvertServiceMessageValue(packageId),
                    ConvertServiceMessageValue(packageVersion),
                    ConvertServiceMessageValue(packageHash),
                    ConvertServiceMessageValue(packageFullPath),
                    ConvertServiceMessageValue(packageFileExtension));

            }

            public static void DeltaVerification(string remotePath, string hash, long size)
            {
                VerboseFormat("##octopus[deltaVerification remotePath=\"{0}\" hash=\"{1}\" size=\"{2}\"]",
                    ConvertServiceMessageValue(remotePath),
                    ConvertServiceMessageValue(hash),
                    ConvertServiceMessageValue(size.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }
}