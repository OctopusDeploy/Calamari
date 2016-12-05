using System;
using System.IO;
using Calamari.Integration.Processes;
using NUnit.Framework;

namespace Calamari.IntegrationTests.Helpers
{
    /// <summary>
    /// Allows intercepting of Log messages by overriding the static `Log` class. 
    /// Note that as the underlying class is static, this helper is NOT THREADSAFE
    /// </summary>
    public class ProxyLog : IDisposable
    {
        private readonly IndentedTextWriter initialStdOut;
        private readonly IndentedTextWriter initialStdErr;
        public ProxyLog()
        {
            initialStdOut = Log.StdOut;
            initialStdErr = Log.StdErr;

            Log.StdOut = new IndentedTextWriter(new SplitTextWriter(Console.Out, stdOut));
            Log.StdErr = new IndentedTextWriter(new SplitTextWriter(Console.Error, stdErr));

        }

        private readonly StringWriter stdOut = new StringWriter();
        private readonly StringWriter stdErr = new StringWriter();


        public string StdOut => stdOut.ToString();
        public string StdErr => stdErr.ToString();

        public void WriteOutput(ICommandOutput commandOutput)
        {
            string line;
            using (var reader = new StringReader(StdOut))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    commandOutput.WriteInfo(line);
                }
            }

            using (var reader = new StringReader(StdErr))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    commandOutput.WriteError(line);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            Log.StdOut = initialStdOut;
            Log.StdErr = initialStdErr;
        }

        public void AssertContains(string expectedOutput)
        {
            Assert.That(StdOut.IndexOf(expectedOutput, StringComparison.OrdinalIgnoreCase) >= 0, string.Format("Expected to find: {0}. Output:\r\n{1}", expectedOutput, StdOut));
        }

        public void AssertDoesNotContain(string expectedOutput)
        {
            Assert.That(StdOut.IndexOf(expectedOutput, StringComparison.OrdinalIgnoreCase) == -1, string.Format("Expected not to find: {0}. Output:\r\n{1}", expectedOutput, StdOut));
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}