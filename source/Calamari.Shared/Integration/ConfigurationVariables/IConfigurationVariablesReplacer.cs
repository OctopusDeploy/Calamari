using Calamari.Common.Plumbing.Variables;
using Octostache;

namespace Calamari.Integration.ConfigurationVariables
{
    public interface IConfigurationVariablesReplacer
    {
        void ModifyConfigurationFile(string configurationFilePath, IVariables variables);
    }
}