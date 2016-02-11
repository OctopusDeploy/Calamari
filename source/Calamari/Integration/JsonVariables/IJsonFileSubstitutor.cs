using Octostache;

namespace Calamari.Integration.JsonVariables
{
    public interface IJsonFileSubstitutor
    {
        void ModifyJsonFile(string jsonFilePath, VariableDictionary variables);
    }
}