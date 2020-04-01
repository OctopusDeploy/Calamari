using System.Collections.Generic;

namespace Calamari.Integration.Proxies
{
    public class ProxyEnvironmentVariablesGeneratorWrapper : IProxyEnvironmentVariablesGenerator
    {
        public IEnumerable<EnvironmentVariable> GenerateProxyEnvironmentVariables()
        {
            return ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables();
        }
    }
}