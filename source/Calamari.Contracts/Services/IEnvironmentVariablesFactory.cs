using System.Collections.Generic;

namespace Calamari.Contracts.Services
{
    public interface IEnvironmentVariablesFactory
    {
        IEnumerable<EnvironmentVariable> GenerateProxyEnvironmentVariables();
    }
}