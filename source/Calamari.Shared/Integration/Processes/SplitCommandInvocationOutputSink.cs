using System.Collections.Generic;

namespace Calamari.Integration.Processes
{
    public class SplitCommandInvocationOutputSink : ICommandInvocationOutputSink
    {
        private readonly List<ICommandInvocationOutputSink> outputs;

        public SplitCommandInvocationOutputSink(params ICommandInvocationOutputSink[] outputs) : this(new List<ICommandInvocationOutputSink>(outputs))
        {
        }

        public SplitCommandInvocationOutputSink(List<ICommandInvocationOutputSink> outputs)
        {
            this.outputs = outputs;
        }

        public void WriteInfo(string line)
        {
            foreach (var output in outputs) output.WriteInfo(line);
        }

        public void WriteError(string line)
        {
            foreach (var output in outputs) output.WriteError(line);
        }
    }
}