using System.Collections.Generic;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Server.Contracts.Actions.Templates
{
    public class ControlType
    {
        public static readonly ControlType SingleLineText = new ControlType("SingleLineText");
        public static readonly ControlType MultiLineText = new ControlType("MultiLineText");
        public static readonly ControlType Select = new ControlType("Select");
        public static readonly ControlType Checkbox = new ControlType("Checkbox");
        public static readonly ControlType Sensitive = new ControlType("Sensitive", VariableType.Sensitive);
        public static readonly ControlType StepName = new ControlType("StepName");
        public static readonly ControlType AzureAccount = new ControlType("AzureAccount", VariableType.AzureAccount);
        public static readonly ControlType Certificate = new ControlType("Certificate", VariableType.Certificate);
        public static readonly ControlType WorkerPool = new ControlType("WorkerPool", VariableType.WorkerPool);
        public static readonly ControlType AmazonWebServicesAccount = new ControlType("AmazonWebServicesAccount", VariableType.AmazonWebServicesAccount);

        public ControlType(string name)
            :this (name, VariableType.String)
        {
        }

        public ControlType(string name, VariableType variableType)
        {
            Name = name;
            VariableType = variableType;
        }

        public string Name { get; set; }
        public VariableType VariableType { get; set; }
        protected bool Equals(ControlType other)
        {
            return Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ControlType) obj);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }
    }
}