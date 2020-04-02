using System.Collections.Generic;
using Calamari.Contracts.Services;
using Calamari.Integration.Proxies;

namespace Calamari.Contracts
{
    public class ProxyEnvironmentVariablesGeneratorWrapper : IProxyEnvironmentVariablesGenerator
    {
        public IEnumerable<EnvironmentVariable> GenerateProxyEnvironmentVariables()
        {
            return ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables();
        }
    }
}