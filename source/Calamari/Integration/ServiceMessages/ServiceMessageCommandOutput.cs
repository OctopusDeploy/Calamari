using Calamari.Extensibility;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.ServiceMessages
{
    /// <summary>
    /// Parses command-output for service-messages
    /// </summary>
    public class ServiceMessageCommandOutput : ICommandOutput
    {
        private readonly IVariableDictionary variables;
        readonly ServiceMessageParser serviceMessageParser;

        public ServiceMessageCommandOutput(IVariableDictionary variables)
        {
            this.variables = variables;
            this.serviceMessageParser = new ServiceMessageParser(ProcessServiceMessage);
        }

        public void WriteInfo(string line)
        {
            serviceMessageParser.Parse(line);
        }

        public void WriteError(string line)
        {
        }

        private void ProcessServiceMessage(ServiceMessage message)
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