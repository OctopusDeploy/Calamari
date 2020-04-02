using System.Collections.Generic;
using System.Linq;
using Calamari.Integration.Proxies;

namespace Calamari.Integration
{
    public class EnvironmentVariablesFactory : IEnvironmentVariablesFactory
    {
        public Dictionary<string, string> GetDefaultEnvironmentVariables()
            =>  ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables().ToDictionary(e => e.Key, e => e.Value);
    }
}