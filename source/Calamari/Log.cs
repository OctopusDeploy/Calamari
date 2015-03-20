using System;
using System.CodeDom.Compiler;

namespace Calamari
{
    public class Log
    {
        static string stdOutMode;
        static readonly IndentedTextWriter StdOut;
        static readonly IndentedTextWriter StdErr;
        static readonly object Sync = new object();

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

        public static void VerboseFormat(string messageFormat, params object[] args)
        {
            Verbose(String.Format(messageFormat, args));
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
            Verbose(String.Format(messageFormat, args));
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
            Error(String.Format(messageFormat, args));
        }
    }
}