using System;

namespace Calamari.Commands.Support
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class CommandAttribute : Attribute, ICommandMetadata
    {
        public CommandAttribute(string name) //, params string[] aliases)
        {
            Name = name;
        }

        public string Name { get; set; }
        public string Description { get; set; }
    }
}