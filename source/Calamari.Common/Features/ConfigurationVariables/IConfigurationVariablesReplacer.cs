using System;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.ConfigurationVariables
{
    public interface IConfigurationVariablesReplacer
    {
        void ModifyConfigurationFile(string configurationFilePath, IVariables variables);
    }
}