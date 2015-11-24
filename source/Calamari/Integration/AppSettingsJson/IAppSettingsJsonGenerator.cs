using Octostache;

namespace Calamari.Integration.AppSettingsJson
{
    public interface IAppSettingsJsonGenerator
    {
        void Generate(string appSettingsFilePath, VariableDictionary variables);
    }
}