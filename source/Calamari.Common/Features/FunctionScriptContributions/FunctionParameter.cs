using System;

namespace Calamari.Common.Features.FunctionScriptContributions
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
}