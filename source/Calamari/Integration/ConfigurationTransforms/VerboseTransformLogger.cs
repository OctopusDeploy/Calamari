using System;
using System.CodeDom.Compiler;
using Microsoft.Web.XmlTransform;

namespace Calamari.Integration.ConfigurationTransforms
{
    public class VerboseTransformLogger : IXmlTransformationLogger
    {
        public event LogDelegate Warning;

        private readonly bool suppressWarnings;
        private string stdOutMode;
        private readonly IndentedTextWriter stdOut;
        private readonly IndentedTextWriter stdErr;

        public VerboseTransformLogger(bool suppressWarnings = false)
        {
            this.suppressWarnings = suppressWarnings;
            stdOut = new IndentedTextWriter(Console.Out, "  ");
            stdErr = new IndentedTextWriter(Console.Error, "  ");
        }

        public void LogMessage(string message, params object[] messageArgs)
        {
            EnsureStdOutMode("verbose");
            stdOut.WriteLine(message, messageArgs);
        }

        public void LogMessage(MessageType type, string message, params object[] messageArgs)
        {
            LogMessage(message, messageArgs);
        }

        public void LogWarning(string message, params object[] messageArgs)
        {
            if (Warning != null) { Warning(this, new ErrorDelegateArgs(string.Format(message, messageArgs))); }

            EnsureStdOutMode(suppressWarnings ? "verbose" : "warning");
            stdOut.WriteLine(message, messageArgs);
        }

        public void LogWarning(string file, string message, params object[] messageArgs)
        {
            if (Warning != null) { Warning(this, new ErrorDelegateArgs(string.Format("{0}: {1}", file, string.Format(message, messageArgs)))); }

            EnsureStdOutMode(suppressWarnings ? "verbose" : "warning");
            stdOut.Write("File {0}: ", file);
            stdOut.WriteLine(message, messageArgs);

        }

        public void LogWarning(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
        {
            if (Warning != null) { Warning(this, new ErrorDelegateArgs(string.Format("{0}({1},{2}): {3}", file, lineNumber, linePosition, string.Format(message, messageArgs)))); }

            EnsureStdOutMode(suppressWarnings ? "verbose" : "warning");
            stdOut.Write("File {0}, line {1}, position {2}: ", file, lineNumber, linePosition);
            stdOut.WriteLine(message, messageArgs);
        }

        public void LogError(string message, params object[] messageArgs)
        {
            stdErr.WriteLine(message, messageArgs);
        }

        public void LogError(string file, string message, params object[] messageArgs)
        {
            stdErr.Write("File {0}: ", file);
            stdErr.WriteLine(message, messageArgs);
        }

        public void LogError(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
        {
            stdErr.Write("File {0}, line {1}, position {2}: ", file, lineNumber, linePosition);
            stdErr.WriteLine(message, messageArgs);
        }

        public void LogErrorFromException(Exception ex)
        {
            stdErr.WriteLine(ex.ToString());
        }

        public void LogErrorFromException(Exception ex, string file)
        {
            stdErr.Write("File {0}: ", file);
            stdErr.WriteLine(ex.ToString());
        }

        public void LogErrorFromException(Exception ex, string file, int lineNumber, int linePosition)
        {
            stdErr.Write("File {0}, line {1}, position {2}: ", file, lineNumber, linePosition);
            stdErr.WriteLine(ex.ToString());
        }

        public void StartSection(string message, params object[] messageArgs)
        {
            EnsureStdOutMode("verbose");
            stdOut.WriteLine(message, messageArgs);
            stdErr.Indent++;
            stdOut.Indent++;
        }

        public void StartSection(MessageType type, string message, params object[] messageArgs)
        {
            StartSection(message, messageArgs);
        }

        public void EndSection(string message, params object[] messageArgs)
        {
            stdErr.Indent--;
            stdOut.Indent--;
            EnsureStdOutMode("verbose");
            stdOut.WriteLine(message, messageArgs);
        }

        public void EndSection(MessageType type, string message, params object[] messageArgs)
        {
            EndSection(message, messageArgs);
        }

        void EnsureStdOutMode(string mode)
        {
            if (stdOutMode != mode)
                stdOut.WriteLine("##octopus[stdout-" + mode + "]");
            stdOutMode = mode;
        }
    }

    public delegate void LogDelegate(object sender, ErrorDelegateArgs args);

    public class ErrorDelegateArgs
    {
        public string Message { get; set; }

        public ErrorDelegateArgs(string message)
        {
            Message = message;
        }
    }
}