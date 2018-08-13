using System;

namespace Calamari.Shared.Commands
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class DeploymentActionAttribute : Attribute, ICommandMetadata
    {
        public DeploymentActionAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
        public string Description { get; set; }
    }
    
    
    //TODO: Not sure we need this?
    public interface ICommandMetadata
    {
        string Name { get; }
        string Description { get; }
    }
}