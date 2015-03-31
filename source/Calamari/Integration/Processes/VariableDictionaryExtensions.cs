using System;
using Calamari.Deployment;
using Octostache;

namespace Calamari.Integration.Processes
{
    public static class VariableDictionaryExtensions
    {
        public static void EnrichWithEnvironmentVariables(this VariableDictionary variables)
        {
            var environmentVariables = Environment.GetEnvironmentVariables();

            foreach (var name in environmentVariables.Keys)
            {
                variables["env:" + name] = (environmentVariables[name] ?? string.Empty).ToString();
            }

            variables.Set(SpecialVariables.Tentacle.Agent.InstanceName, "#{env:TentacleInstanceName}");
        }


        public static void SetOutputVariable(this VariableDictionary variables, string name, string value)
        {
            var actionName = variables.Get(SpecialVariables.Action.Name);

            if (string.IsNullOrWhiteSpace(actionName))
                return;

            var actionScopedVariable = SpecialVariables.GetOutputVariableName(actionName, name);

            variables.Set(actionScopedVariable, value);

            var machineName = variables.Get(SpecialVariables.Machine.Name);

            if (string.IsNullOrWhiteSpace(machineName)) 
                return;

            var machineIndexedVariableName = SpecialVariables.GetMachineIndexedOutputVariableName(actionName, machineName, name);
            variables.Set(machineIndexedVariableName, value);
        }
    }
}