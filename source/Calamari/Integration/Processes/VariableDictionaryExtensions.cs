using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Octostache;

namespace Calamari.Integration.Processes
{

    public class CalamariVariableDictionary : VariableDictionary
    {
        protected List<string> SensitiveVariableNames = new List<string>();

        public CalamariVariableDictionary() { }

        public CalamariVariableDictionary(string storageFilePath) : base(storageFilePath) { }

        public CalamariVariableDictionary(string storageFilePath, string sensitiveFilePath, string sensitiveFilePassword, string sensitiveFileSalt)
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            if (!string.IsNullOrEmpty(storageFilePath))
            {
                if (!fileSystem.FileExists(storageFilePath))
                    throw new CommandException("Could not find variables file: " + storageFilePath);

                Log.Info("Using variables from: " + storageFilePath);
                var nonSensitiveVariables =  new VariableDictionary(storageFilePath);
                nonSensitiveVariables.GetNames().ForEach(name => Set(name, nonSensitiveVariables[name]));
            }

            if (!string.IsNullOrEmpty(sensitiveFilePath))
            {
                if (!fileSystem.FileExists(sensitiveFilePath))
                    throw new CommandException("Could not find variables file: " + sensitiveFilePath);

                if (string.IsNullOrWhiteSpace(sensitiveFilePassword))
                    throw new CommandException("sensitiveVariablesPassword option must be supplied if sensitiveVariables option is supplied.");

                if (string.IsNullOrWhiteSpace(sensitiveFileSalt))
                    throw new CommandException("sensitiveVariablesSalt option must be supplied if sensitiveVariables option is supplied.");

                Log.Info("Using sensitive variables from: " + sensitiveFilePath);
                var sensitiveVariableDictionary = new SensitiveVariables(fileSystem).IncludeSensitiveVariables(sensitiveFilePath, sensitiveFilePassword, sensitiveFileSalt);
                sensitiveVariableDictionary.GetNames().ForEach(name => Set(name, sensitiveVariableDictionary[name]));
            }
        }

        public void SetSensitive(string name, string value)
        {
            if (name == null) return;
            Set(name, value);
            SensitiveVariableNames.Add(name);
        }

        public bool IsSensitive(string name)
        {
            return name != null && SensitiveVariableNames.Contains(name);
        }
    }

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
            variables.Set(name, value);

            // And set the output-variables.
            // Assuming we are running in a step named 'DeployWeb' and are setting a variable named 'Foo'
            // then we will set Octopus.Action[DeployWeb].Output.Foo
            var actionName = variables.Get(SpecialVariables.Action.Name);

            if (string.IsNullOrWhiteSpace(actionName))
                return;

            var actionScopedVariable = SpecialVariables.GetOutputVariableName(actionName, name);

            variables.Set(actionScopedVariable, value);

            // And if we are on a machine named 'Web01'
            // Then we will set Octopus.Action[DeployWeb].Output[Web01].Foo
            var machineName = variables.Get(SpecialVariables.Machine.Name);

            if (string.IsNullOrWhiteSpace(machineName))
                return;

            var machineIndexedVariableName = SpecialVariables.GetMachineIndexedOutputVariableName(actionName, machineName, name);
            variables.Set(machineIndexedVariableName, value);
        }



        public static void LogVariables(this VariableDictionary variables)
        {
            if (variables.GetFlag(SpecialVariables.PrintVariables))
            {
                Log.Verbose("The following variables are available:" + Environment.NewLine + variables.ToString(IsPrintable, true));
            }

            if (variables.GetFlag(SpecialVariables.PrintEvaluatedVariables))
            {
                Log.Verbose("The following evaluated variables are available:" + Environment.NewLine + variables.ToString(IsPrintable, false));
            }
        }

        private static string ToString(this VariableDictionary variables, Func<string, bool> nameFilter, bool useRawValue)
        {
            var text = new StringBuilder();

            foreach (var name in variables.GetNames())
            {
                if (!nameFilter(name))
                    continue;

                text.AppendFormat("[{0}] = '{1}'", name, useRawValue ? variables.GetRaw(name) : variables.Get(name));
                text.AppendLine();
            }

            return text.ToString();
        }

        private static bool IsPrintable(string variableName)
        {
            return !variableName.Contains("CustomScripts.");
        }
    }
}
