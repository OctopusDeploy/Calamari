using System;
using Octostache;

namespace Calamari.Integration.Processes
{
    public static class VariableDictionaryExtensions
    {
        public static void EnrichWithEnvironmentVariables(this VariableDictionary variables)
        {
            var environmentVariables = Environment.GetEnvironmentVariables();

            foreach (var name in environmentVariables.Keys)
            {
                variables["env:" + name] = (environmentVariables[name] ?? string.Empty).ToString();
            }
        }
    }
}