using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Common.Features.Processes
{
    public class SplitCommandInvocationOutputSink : ICommandInvocationOutputSink
    {
        readonly List<ICommandInvocationOutputSink> outputs;

        public SplitCommandInvocationOutputSink(params ICommandInvocationOutputSink[] outputs)
            : this(new List<ICommandInvocationOutputSink>(outputs))
        {
        }

        public SplitCommandInvocationOutputSink(List<ICommandInvocationOutputSink> outputs)
        {
            this.outputs = outputs;
        }

        public void WriteInfo(string line)
        {
            foreach (var output in outputs)
                output.WriteInfo(line);
        }

        public void WriteError(string line)
        {
            foreach (var output in outputs)
                output.WriteError(line);
        }
    }
}