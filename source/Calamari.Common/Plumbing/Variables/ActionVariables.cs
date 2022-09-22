using System;

namespace Calamari.Common.Plumbing.Variables
{
    public static class ActionVariables
    {
        public const string Name = "Octopus.Action.Name";
        public const string AdditionalPaths = "Octopus.Action.AdditionalPaths";

        public static readonly string StructuredConfigurationVariablesTargets = "Octopus.Action.Package.JsonConfigurationVariablesTargets";
        /* If this flag still exists after 2020.5.0 releases, please reach out to those involved with adding the fallback flag for
         Structured Configuration in this PR (https://github.com/OctopusDeploy/Calamari/pull/629) so we can assess if the feature
         has been stable for long enough to tidy up the fallback flag. */
        public static readonly string StructuredConfigurationFallbackFlag = "Octopus.Action.StructuredConfigurationFallbackFlag";

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