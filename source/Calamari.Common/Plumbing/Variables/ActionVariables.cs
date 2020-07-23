using System;

namespace Calamari.Common.Plumbing.Variables
{
    public static class ActionVariables
    {
        public const string Name = "Octopus.Action.Name";
        public const string AdditionalPaths = "Octopus.Action.AdditionalPaths";
        
        /* We've renamed these two variables from "Json*" to "Structured*" as they're being used with the renamed config feature
         Structured Configuration Variables. There are issues around supporting two variables when re-naming so for the time being
         we're going to continue using the old octopus variable names. */
        public static readonly string StructuredConfigurationVariablesEnabled = "Octopus.Action.Package.JsonConfigurationVariablesEnabled";
        public static readonly string StructuredConfigurationVariablesTargets = "Octopus.Action.Package.JsonConfigurationVariablesTargets";
        public static readonly string StructuredConfigurationFeatureFlag = "Octopus.Action.StructuredConfigurationFeatureFlag";

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