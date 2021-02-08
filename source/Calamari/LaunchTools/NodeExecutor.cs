using System;

namespace Calamari.LaunchTools
{
    [LaunchTool(LaunchTools.Node)]
    public class NodeExecutor : LaunchTool<NodeInstructions>
    {
        // Depending on environment, I can hardcode the execution path of Node here.

        //\node10.

        protected override int ExecuteInternal(NodeInstructions instructions, string inputs, params string[] args)
        {
            throw new NotImplementedException();
        }
    }

    public class NodeInstructions
    {
        public string NodePathVariable { get; set; }
        public string TargetPathVariable { get; set; }
        public string TargetEntryPoint { get; set; }
    }
}