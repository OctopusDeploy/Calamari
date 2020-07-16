using System;

namespace Calamari.Common.Plumbing.Variables
{
    public static class ActionVariables
    {
        public const string Name = "Octopus.Action.Name";
        public const string AdditionalPaths = "Octopus.Action.AdditionalPaths";
        public static readonly string StructuredConfigurationVariablesEnabled = "Octopus.Action.StructuredConfigurationVariablesEnabled";
        public static readonly string StructuredConfigurationVariablesTargets = "Octopus.Action.StructuredConfigurationVariablesTargets";

        public static string GetOutputVariableName(string actionName, string variableName)
        {
            return string.Format("Octopus.Action[{0}].Output.{1}", actionName, variableName);
        }

        public static string GetMachineIndexedOutputVariableName(string actionName, string machineName, string variableName)
        {
            return string.Format("Octopus.Action[{0}].Output[{1}].{2}", actionName, machineName, variableName);
        }
    }
}