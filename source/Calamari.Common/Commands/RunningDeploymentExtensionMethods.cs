using System;

namespace Calamari.Common.Commands
{
    public static class RunningDeploymentExtensionMethods
    {
        public static string GetNonSensitiveVariable(this RunningDeployment deployment, string variableName)
        {
            var result = deployment.Variables.Get(variableName, out var error);
            if (!string.IsNullOrEmpty(error))
            {
                var message = $"Unable to evaluate '{variableName}'. It may reference missing or sensitive values.";
                throw new InvalidOperationException(message);
            }

            return result;
        }
        
        public static string GetMandatoryNonSensitiveVariable(this RunningDeployment deployment, string variableName)
        {
            var result = deployment.Variables.Get(variableName, out var error) ?? null;
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new CommandException($"Variable {variableName} was not supplied");
            }
            
            if (!string.IsNullOrEmpty(error))
            {
                var message = $"Unable to evaulate '{variableName}'. It may reference missing or sensitive values.";
                throw new InvalidOperationException(message);
            }

            return result;
        }
    }
}