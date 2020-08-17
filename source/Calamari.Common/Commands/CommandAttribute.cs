using System;

namespace Calamari.Common.Commands
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class CommandAttribute : Attribute
    {
        public CommandAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
        public string? Description { get; set; }
    }
}