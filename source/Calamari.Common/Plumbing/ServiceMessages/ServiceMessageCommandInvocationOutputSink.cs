using System;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Plumbing.ServiceMessages
{
    /// <summary>
    /// Parses command-output for service-messages
    /// </summary>
    public class ServiceMessageCommandInvocationOutputSink : ICommandInvocationOutputSink
    {
        readonly IVariables variables;
        readonly ServiceMessageParser serviceMessageParser;

        public ServiceMessageCommandInvocationOutputSink(IVariables variables)
        {
            this.variables = variables;
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

                    if (!string.IsNullOrWhiteSpace(variableName))
                        variables.SetOutputVariable(variableName, variableValue);
                    break;
            }
        }
    }
}