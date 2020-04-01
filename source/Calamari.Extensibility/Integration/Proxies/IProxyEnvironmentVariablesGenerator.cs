using System.Collections.Generic;

namespace Calamari.Integration.Proxies
{
    public interface IProxyEnvironmentVariablesGenerator
    {
        IEnumerable<EnvironmentVariable> GenerateProxyEnvironmentVariables();
    }
}