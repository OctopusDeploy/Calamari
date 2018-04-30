using System;

namespace Calamari.Tests
{
    public static class EnvironmentVariables
    {
        public static readonly string[] EnvironmentVars = new[]
        {
            "AWS.E2E.AccessKeyId",
            "AWS.E2E.SecretKeyId",
            "Azure.E2E.TenantId",
            "Azure.E2E.ClientId",
            "Azure.E2E.Password",
            "Azure.E2E.SubscriptionId"
        };

        public static void EnsureVariablesExist()
        {
            foreach (var environmentVar in EnvironmentVars)
            {
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(environmentVar)))
                {
                    Log.Error("Could not find the environment variable " + environmentVar);
                }

            }
        }
    }
}
