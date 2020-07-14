namespace Calamari.CommonTemp
{
    internal interface IJsonConfigurationVariableReplacer
    {
        void ModifyJsonFile(string jsonFilePath, IVariables variables);
    }
}