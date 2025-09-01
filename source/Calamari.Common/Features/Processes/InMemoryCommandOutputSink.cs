using System;
using System.Text;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Common.Features.Processes
{
    public class InMemoryCommandOutputSink : ICommandInvocationOutputSink
    {
        readonly StringBuilder stdOut = new StringBuilder();
        readonly StringBuilder stdErr = new StringBuilder();
        public string StdOut => stdOut.ToString();

        public void WriteInfo(string line)
        {
            stdOut.AppendLine(line);
        }

        public void WriteError(string line)
        {
            stdErr.AppendLine(line);
        }
    }
}