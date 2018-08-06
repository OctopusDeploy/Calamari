using Octostache;

namespace Calamari.Integration.JsonVariables
{
    public interface IJsonConfigurationVariableReplacer
    {
        void ModifyJsonFile(string jsonFilePath, VariableDictionary variables);
    }
}