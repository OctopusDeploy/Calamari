using Octostache;

namespace Calamari.Features.StructuredVariables
{
    public interface IJsonConfigurationVariableReplacer
    {
        void ModifyJsonFile(string jsonFilePath, IVariables variables);
    }
}