using System.Collections.Generic;

namespace Calamari.Contracts.Services
{
    public interface IProxyEnvironmentVariablesGenerator
    {
        IEnumerable<EnvironmentVariable> GenerateProxyEnvironmentVariables();
    }
}