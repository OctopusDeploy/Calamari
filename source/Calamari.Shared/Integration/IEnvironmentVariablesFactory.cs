using System.Collections.Generic;

namespace Calamari.Integration
{
    public interface IEnvironmentVariablesFactory
    {
        Dictionary<string, string> GetDefaultEnvironmentVariables();
    }
}