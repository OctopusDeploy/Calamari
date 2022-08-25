using System;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class VariableExtensions
    {
        public static PathToPackage? GetPathToPrimaryPackage(this IVariables variables, ICalamariFileSystem fileSystem, bool required)
        {
            var path = variables.Get(TentacleVariables.CurrentDeployment.PackageFilePath);

            if (string.IsNullOrEmpty(path))
                if (required)
                    throw new CommandException($"The `{TentacleVariables.CurrentDeployment.PackageFilePath}` was not specified or blank. This is likely a bug in Octopus, please contact Octopus support.");
                else
                    return null;

            path = CrossPlatform.ExpandPathEnvironmentVariables(path);
            if (!fileSystem.FileExists(path))
                throw new CommandException("Could not find package file: " + path);

            return new PathToPackage(path);
        }

        public static bool IsFeatureEnabled(this IVariables variables, string featureName)
        {
            var features = variables.GetStrings(KnownVariables.Package.EnabledFeatures)
                                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            return features.Contains(featureName);
        }

        public static bool IsPackageRetentionEnabled(this IVariables variables)
        {
            bool.TryParse(variables.Get(KnownVariables.Calamari.EnablePackageRetention, bool.FalseString),  out var retentionEnabled);

            var tentacleHome = variables.Get(TentacleVariables.Agent.TentacleHome);
            var packageRetentionJournalPath = variables.Get(KnownVariables.Calamari.PackageRetentionJournalPath);

            return retentionEnabled && (!string.IsNullOrWhiteSpace(packageRetentionJournalPath) || !string.IsNullOrWhiteSpace(tentacleHome));
        }

        public static void SetOutputVariable(this IVariables variables, string name, string? value)
        {
            variables.Set(name, value);

            // And set the output-variables.
            // Assuming we are running in a step named 'DeployWeb' and are setting a variable named 'Foo'
            // then we will set Octopus.Action[DeployWeb].Output.Foo
            var actionName = variables.Get(ActionVariables.Name);

            if (string.IsNullOrWhiteSpace(actionName))
                return;

            var actionScopedVariable = ActionVariables.GetOutputVariableName(actionName, name);

            variables.Set(actionScopedVariable, value);

            // And if we are on a machine named 'Web01'
            // Then we will set Octopus.Action[DeployWeb].Output[Web01].Foo
            var machineName = variables.Get(MachineVariables.Name);

            if (string.IsNullOrWhiteSpace(machineName))
                return;

            var machineIndexedVariableName = ActionVariables.GetMachineIndexedOutputVariableName(actionName, machineName, name);
            variables.Set(machineIndexedVariableName, value);
        }
    }
}