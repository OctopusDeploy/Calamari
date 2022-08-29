using System;

namespace Calamari.LaunchTools
{
    public enum LaunchTools
    {
        Calamari,
        Node
    }

    public class LaunchToolMeta
    {
        public LaunchTools Tool { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class LaunchToolAttribute : Attribute
    {
        public LaunchToolAttribute(LaunchTools tool)
        {
            Tool = tool;
        }

        public LaunchTools Tool { get; }
    }
}