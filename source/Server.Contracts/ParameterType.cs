using System;

namespace Sashimi.Server.Contracts
{
    public class FunctionParameter
    {
        public FunctionParameter(ParameterType type, string? dependsOn = null)
        {
            Type = type;
            DependsOn = dependsOn;
        }

        public ParameterType Type { get; }
        public string? DependsOn { get; }
    }

    public enum ParameterType
    {
        String,
        Bool,
        Int
    }
}