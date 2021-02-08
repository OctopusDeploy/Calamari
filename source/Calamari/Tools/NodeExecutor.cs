using System;

namespace Calamari.Tools
{
    [ExecutionTool(ExecutionTool.Node)]
    public class NodeExecutor : IExecuteTool
    {
        public int Execute(string command, string inputs, string[] commandLineArguments)
        {
            // TODO: Create node bootstrapper and feed args.

            throw new NotImplementedException();
        }
    }
}