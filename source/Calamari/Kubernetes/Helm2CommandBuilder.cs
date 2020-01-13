using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Kubernetes.Conventions;
using Newtonsoft.Json;
using Octostache;

namespace Calamari.Kubernetes
{
    public class Helm2CommandBuilder : HelmCommandBuilder, IHelmCommandBuilder
    {

        public IHelmCommandBuilder WithCommand(string command)
        {
            CommandStringBuilder.Append(command);
            return this;
        }

        public IHelmCommandBuilder Debug()
        {
            CommandStringBuilder.Append(" --debug");
            return this;
        }
        
        public IHelmCommandBuilder Namespace(string @namespace)
        {
            CommandStringBuilder.Append($" --namespace \"{@namespace}\"");
            return this;
        }

        public IHelmCommandBuilder NamespaceFromSpecialVariable(RunningDeployment deployment)
        {
            var @namespace = deployment.Variables.Get(SpecialVariables.Helm.Namespace);
            if (!string.IsNullOrWhiteSpace(@namespace))
            {
                Namespace(@namespace);
            }
            return this;
        }

        public IHelmCommandBuilder ResetValues()
        {
            CommandStringBuilder.Append(" --reset-values");
            return this;
        }

        public IHelmCommandBuilder ResetValuesFromSpecialVariableFlag(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(SpecialVariables.Helm.ResetValues, true))
            {
                ResetValues();
            }
            return this;
        }

        public IHelmCommandBuilder TillerTimeout(string tillerTimeout)
        {
            if (!int.TryParse(tillerTimeout, out _))
            {
                throw new CommandException($"Tiller timeout period is not a valid integer: {tillerTimeout}");
            }

            CommandStringBuilder.Append($" --tiller-connection-timeout \"{tillerTimeout}\"");
            return this;
        }

        public IHelmCommandBuilder TillerTimeoutFromSpecialVariable(RunningDeployment deployment)
        {
            if (!deployment.Variables.IsSet(SpecialVariables.Helm.TillerTimeout)) return this;;

            return TillerTimeout(deployment.Variables.Get(SpecialVariables.Helm.TillerTimeout));
        }

        public IHelmCommandBuilder TillerNamespace(string tillerNamespace)
        {
            CommandStringBuilder.Append($" --tiller-namespace \"{tillerNamespace}\"");
            return this;
        }

        public IHelmCommandBuilder TillerNamespaceFromSpecialVariable(RunningDeployment deployment)
        {
            if (deployment.Variables.IsSet(SpecialVariables.Helm.TillerNamespace))
            {
                TillerNamespace(deployment.Variables.Get(SpecialVariables.Helm.TillerNamespace));
            }
            return this;
        }

        public IHelmCommandBuilder Timeout(string timeout)
        {
            if (!int.TryParse(timeout, out _))
            {
                throw new CommandException($"Timeout period is not a valid integer: {timeout}");
            }

            CommandStringBuilder.Append($" --timeout \"{timeout}\"");
            return this;
        }

        public IHelmCommandBuilder TimeoutFromSpecialVariable(RunningDeployment deployment)
        {
            if (!deployment.Variables.IsSet(SpecialVariables.Helm.Timeout)) return this;;
            
            Timeout(deployment.Variables.Get(SpecialVariables.Helm.Timeout));
            return this;
        }

        public IHelmCommandBuilder Values(string value)
        {
            CommandStringBuilder.Append($" --values \"{value}\"");
            return this;
        }

        public IHelmCommandBuilder ValuesFromSpecialVariable(RunningDeployment deployment, ICalamariFileSystem fileSystem)
        {
            foreach (var additionalValuesFile in AdditionalValuesFiles(deployment, fileSystem))
            {
                Values(additionalValuesFile);
            }

            if (TryAddRawValuesYaml(deployment, out var rawValuesFile))
            {
                Values(rawValuesFile);
            }

            if (TryGenerateVariablesFile(deployment, out var valuesFile))
            {
                Values(valuesFile);
            }
            return this;
        }

        public IHelmCommandBuilder AdditionalArguments(string additionalArguments)
        {
            CommandStringBuilder.Append($" {additionalArguments}");
            return this;
        }

        public IHelmCommandBuilder AdditionalArgumentsFromSpecialVariable(RunningDeployment deployment)
        {
            var additionalArguments = deployment.Variables.Get(SpecialVariables.Helm.AdditionalArguments);

            if (!string.IsNullOrWhiteSpace(additionalArguments))
            {
                AdditionalArguments(additionalArguments);
            }
            return this;
        }

        public IHelmCommandBuilder Purge()
        {
            CommandStringBuilder.Append(" --purge");
            return this;
        }
        
        public IHelmCommandBuilder Install()
        {
            CommandStringBuilder.Append(" --install");
            return this;
        }

        public IHelmCommandBuilder Home(string homeDirectory)
        {
            CommandStringBuilder.Append($" --home \"{homeDirectory}\"");
            return this;
        }

        public IHelmCommandBuilder ClientOnly()
        {
            CommandStringBuilder.Append(" --client-only");
            return this;
        }

        public IHelmCommandBuilder Version()
        {
            CommandStringBuilder.Append(" --version");
            return this;
        }

        public IHelmCommandBuilder Destination(string destinationDirectory)
        {
            CommandStringBuilder.Append($" --destination \"{destinationDirectory}\"");
            
            return this;
        }

        public IHelmCommandBuilder Username(string username)
        {
            CommandStringBuilder.Append($" --username \"{username}\"");
            
            return this;
        }

        public IHelmCommandBuilder Password(string password)
        {
            CommandStringBuilder.Append($" --password \"{password}\"");
            
            return this;
        }
        
        public IHelmCommandBuilder SetExecutable(VariableDictionary variableDictionary)
        {
            HelmExecutable.Clear();
            HelmExecutable.Append(HelmBuilder.HelmExecutable(variableDictionary));
            
            return this;
        }

        public IHelmCommandBuilder Reset()
        {
            CommandStringBuilder.Clear();
            HelmExecutable.Clear();
            HelmExecutable.Append("helm");

            return this;
        }

        public string Build()
        {
            return $"{HelmExecutable} {CommandStringBuilder}";
        }
    }
}