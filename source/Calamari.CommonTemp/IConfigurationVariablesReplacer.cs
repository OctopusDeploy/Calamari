namespace Calamari.CommonTemp
{
    internal interface IConfigurationVariablesReplacer
    {
        void ModifyConfigurationFile(string configurationFilePath, IVariables variables);
    }
}