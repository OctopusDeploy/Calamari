using System.Collections.Generic;

namespace Calamari.Integration.Processes
{
    public class SplitCommandOutput : ICommandOutput
    {
        private readonly List<ICommandOutput> outputs;

        public SplitCommandOutput(params ICommandOutput[] outputs) : this(new List<ICommandOutput>(outputs))
        {
        }

        public SplitCommandOutput(List<ICommandOutput> outputs)
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