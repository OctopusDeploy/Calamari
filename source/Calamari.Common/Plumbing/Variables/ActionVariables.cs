using System;

namespace Calamari.Common.Plumbing.Variables
{
    public static class ActionVariables
    {
        public const string Name = "Octopus.Action.Name";
        public const string AdditionalPaths = "Octopus.Action.AdditionalPaths";
        
        /* We've renamed these two variables from "Json*" to "Structured*" as they're being used with the renamed config feature
         Structured Configuration Variables. TThese values appear in deployment process resources sent between clients and the API, 
         so until we have support for API versioning (https://github.com/OctopusDeploy/Architecture/pull/11), it is not feasible to 
         change their values without causing a breaking change. For this reason, for the time being we're going to continue using 
         the old Octopus Variable names. */
        public static readonly string StructuredConfigurationVariablesEnabled = "Octopus.Action.Package.JsonConfigurationVariablesEnabled";
        public static readonly string StructuredConfigurationVariablesTargets = "Octopus.Action.Package.JsonConfigurationVariablesTargets";

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