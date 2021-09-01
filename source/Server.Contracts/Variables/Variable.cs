using System;
using Octopus.Data.Model;

namespace Sashimi.Server.Contracts.Variables
{
    public class Variable
    {
        public Variable(string name, string? value)
            : this(name, value, VariableType.String)
        {
        }

        public Variable(string name, string? value, VariableType type)
        {
            Name = name;
            Value = value;
            Type = type;
        }

        public Variable(string name, SensitiveString? value)
        {
            Name = name;
            Value = value?.Value;
            Type = VariableType.Sensitive;
        }

        public string Name { get; }
        public string? Value { get; }
        public VariableType Type { get; }
    }
}