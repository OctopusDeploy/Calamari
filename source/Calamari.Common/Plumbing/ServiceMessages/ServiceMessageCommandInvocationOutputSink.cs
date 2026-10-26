using System;
using System.Linq;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Plumbing.ServiceMessages
{
    /// <summary>
    /// Parses command-output for service-messages
    /// </summary>
    public class ServiceMessageCommandInvocationOutputSink : ICommandInvocationOutputSink
    {
        readonly IVariables variables;
        readonly Func<string, bool>? isTrustedOutputVariable;
        readonly ServiceMessageParser serviceMessageParser;

        public ServiceMessageCommandInvocationOutputSink(IVariables variables, Func<string, bool>? isTrustedOutputVariable = null)
        {
            this.variables = variables;
            this.isTrustedOutputVariable = isTrustedOutputVariable;
            serviceMessageParser = new ServiceMessageParser(ProcessServiceMessage);
        }

        public void WriteInfo(string line)
        {
            serviceMessageParser.Parse(line);
        }

        public void WriteError(string line)
        {
        }

        void ProcessServiceMessage(ServiceMessage message)
        {
            switch (message.Name)
            {
                case ServiceMessageNames.SetVariable.Name:
                    var variableName = message.GetValue(ServiceMessageNames.SetVariable.NameAttribute);
                    var variableValue = message.GetValue(ServiceMessageNames.SetVariable.ValueAttribute);

                    if (string.IsNullOrWhiteSpace(variableName))
                        break;

                    if (CanSetStepVariable(variableName))
                    {
                        variables.SetOutputVariable(variableName, variableValue);
                    }
                    else
                    {
                        variables.SetActionScopedOutputVariable(variableName, variableValue);
                        Log.Verbose($"Output variable '{variableName}' is available to later steps as Octopus.Action[<step name>].Output.{variableName}. To also use it as '{variableName}' later in this step, add it to {KnownVariables.Calamari.AllowOutputVariableOverwrite}.");
                    }

                    break;
            }
        }

        bool CanSetStepVariable(string name)
            => name.Equals(KnownVariables.Action.SkipRemainingConventions, StringComparison.OrdinalIgnoreCase)
               || (isTrustedOutputVariable?.Invoke(name) ?? false)
               || variables.GetStrings(KnownVariables.Calamari.AllowOutputVariableOverwrite, ',').Any(n => n.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
