using System;

namespace Calamari.Tools
{
    public enum ExecutionTool
    {
        Calamari,
        Node
    }

    public class ExecutionToolMeta
    {
        public ExecutionTool Tool { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ExecutionToolAttribute : Attribute
    {
        public ExecutionToolAttribute(ExecutionTool tool)
        {
            Tool = tool;
        }

        public ExecutionTool Tool { get; }
    }
}