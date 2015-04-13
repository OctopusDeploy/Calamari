using System.Collections.Generic;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;
using Octostache;

namespace Calamari.Tests.Helpers
{
    public class CaptureCommandOutput : ICommandOutput
    {
        readonly List<string> infos = new List<string>();
        readonly List<string> errors = new List<string>();
        readonly ServiceMessageParser serviceMessageParser;
        readonly VariableDictionary outputVariables = new VariableDictionary();

        public CaptureCommandOutput()
        {
            serviceMessageParser = new ServiceMessageParser(ParseServiceMessage);
        }

        public void WriteInfo(string line)
        {
            serviceMessageParser.Parse(line);
            infos.Add(line);
        }

        public void WriteError(string line)
        {
            errors.Add(line);
        }

        public VariableDictionary OutputVariables
        {
            get { return outputVariables; }
        }

        public IList<string> Infos
        {
            get { return infos; }
        }

        public IList<string> Errors
        {
            get { return errors; }
        }

        void ParseServiceMessage(ServiceMessage message)
        {
            switch (message.Name)
            {
                case ServiceMessageNames.SetVariable.Name:
                    var variableName = message.GetValue(ServiceMessageNames.SetVariable.NameAttribute);
                    var variableValue = message.GetValue(ServiceMessageNames.SetVariable.ValueAttribute);

                    if (!string.IsNullOrWhiteSpace(variableName))
                        outputVariables.Set(variableName, variableValue);
                    break;
            }
        }
    }
}