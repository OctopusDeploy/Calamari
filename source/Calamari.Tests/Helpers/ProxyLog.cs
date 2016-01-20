using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Calamari.Tests.Helpers
{
    /// <summary>
    /// Allows intercepting of Log messages by overriding the static `Log` class. 
    /// Note that as the underlying class is static, this helper is NOT THREADSAFE
    /// </summary>
    public class ProxyLog : IDisposable
    {
        private static bool isActive = false;
        readonly IndentedTextWriter originalStdOut;
        readonly IndentedTextWriter originalStdErr;

        readonly StringBuilder interceptedOutWriter;
        readonly StringBuilder interceptedErrWriter;
        public ProxyLog()
        {
            if (isActive)
            {
                throw new Exception("You are already intercepting the Logs. Please dispose of existing ProxyLog");
            }
            isActive = true;

            originalStdOut = Log.StdOut;
            interceptedOutWriter = new StringBuilder();
            Log.StdOut = new IndentedTextWriter(new StringWriter(interceptedOutWriter));

            originalStdErr = Log.StdErr;
            interceptedErrWriter = new StringBuilder();
            Log.StdErr = new IndentedTextWriter(new StringWriter(interceptedErrWriter));
        }

        public string StdOut
        {
            get { return interceptedOutWriter.ToString(); }
        }

        public string StdErr
        {
            get { return interceptedErrWriter.ToString(); }
        }

        public void Dispose()
        {
            Log.StdOut = originalStdOut;
            Log.StdErr = originalStdErr;

            isActive = false;
        }

        public void AssertContains(string expectedOutput)
        {
            Assert.That(StdOut.IndexOf(expectedOutput, StringComparison.OrdinalIgnoreCase) >= 0, string.Format("Expected to find: {0}. Output:\r\n{1}", expectedOutput, StdOut));
        }

        public void AssertDoesNotContain(string expectedOutput)
        {
            Assert.That(StdOut.IndexOf(expectedOutput, StringComparison.OrdinalIgnoreCase) == -1, string.Format("Expected not to find: {0}. Output:\r\n{1}", expectedOutput, StdOut));
        }
    }
}