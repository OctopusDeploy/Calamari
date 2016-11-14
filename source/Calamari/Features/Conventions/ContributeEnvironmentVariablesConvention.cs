using System;
using Calamari.Shared;
using Calamari.Shared.Convention;

namespace Calamari.Features.Conventions
{
    [ConventionMetadata(CommonConventions.ContributeEnvironmentVariables, "Adds Environment Variables to Dictionary")]
    public class ContributeEnvironmentVariablesConvention : IInstallConvention
    {
        public void Install(IVariableDictionary variables)
        {
            //TODO: This already exists elsewher in EnrichWithEnvironmentVariables();
            var environmentVariables = Environment.GetEnvironmentVariables();
            foreach (var name in environmentVariables.Keys)
            {
                variables["env:" + name] = (environmentVariables[name] ?? string.Empty).ToString();
            }

            variables.Set(SpecialVariables.Tentacle.Agent.InstanceName, "#{env:TentacleInstanceName}");
        }
    }
}