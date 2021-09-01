using System;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public class OutputVariable
    {
        public OutputVariable(string name, string? value, bool isSensitive = false)
        {
            Name = name;
            Value = value;
            IsSensitive = isSensitive;
        }

        public string Name { get; }
        public string? Value { get; }
        public bool IsSensitive { get; }
    }
}