using System;
using System.Collections.Generic;

namespace Calamari.Tests.Helpers;

public sealed class EnvironmentVariableSetter : IDisposable
{
    private readonly Func<IEnumerable<KeyValuePair<string, string>>> newEnvironmentVariables;
    private readonly IReadOnlyDictionary<string,string> oldEnvironmentVariables;

    public EnvironmentVariableSetter(Func<IEnumerable<KeyValuePair<string,string>>> newEnvironmentVariables)
    {
        this.newEnvironmentVariables = newEnvironmentVariables;
        var oldEnvVars = new Dictionary<string, string>();
        foreach (var envVar in newEnvironmentVariables())
        {
            oldEnvVars[envVar.Key] = Environment.GetEnvironmentVariable(envVar.Key);
            Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
        }

        oldEnvironmentVariables = oldEnvVars;
    }

    public void Dispose()
    {
        foreach (var envVar in newEnvironmentVariables())
        {
            Environment.SetEnvironmentVariable(envVar.Key, oldEnvironmentVariables[envVar.Key]);
        }
    }
}