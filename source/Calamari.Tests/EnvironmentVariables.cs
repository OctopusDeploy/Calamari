using System;
using System.Linq;

namespace Calamari.Tests
{

    public enum ExternalVariable
    {
        [EnvironmentVariable("AWS_OctopusAPITester_Access", "AWS - OctopusAPITester")]
        AwsAcessKey,

        [EnvironmentVariable("AWS_OctopusAPITester_Secret", "AWS - OctopusAPITester")]
        AwsSecretKey,

        [EnvironmentVariable("SSH_OctopusAPITester_PrivateKey", "SSH - OctopusAPITester")]
        SshPrivateKey,

        [EnvironmentVariable("Azure_OctopusAPITester_Password", "Azure - OctopusAPITester")]
        AzureSubscriptionPassword,

        [EnvironmentVariable("Azure_OctopusAPITester_ClientId", "Azure - OctopusAPITester")]
        AzureSubscriptionClientId,

        [EnvironmentVariable("GitHub_OctopusAPITester_Username", "GitHub Test Account")]
        GitHubUsername,

        [EnvironmentVariable("GitHub_OctopusAPITester_Password", "GitHub Test Account")]
        GitHubPassword,
        
        [EnvironmentVariable("Helm_OctopusAPITester_Password", "Helm Password for https://octopusdeploy.jfrog.io")]
        HelmPassword
    }

    public static class ExternalVariables
    {
        public static void LogMissingVariables()
        {
            var missingVariables = Enum.GetValues(typeof(ExternalVariable)).Cast<ExternalVariable>()
                .Select(prop => EnvironmentVariableAttribute.Get(prop))
                .Where(attr => Environment.GetEnvironmentVariable(attr.Name) == null)
                .ToList();

            if (!missingVariables.Any())
                return;

            Log.Warn($"The following environment variables could not be found: " +
                $"\n{string.Join("\n", missingVariables.Select(var => $" - {var.Name}\t\tSource: {var.LastPassName}"))}" +
                $"\n\nTests that rely on these variables are likely to fail.");
        }

        public static string Get(ExternalVariable property)
        {
            var attr = EnvironmentVariableAttribute.Get(property);
            var valueFromEnv = Environment.GetEnvironmentVariable(attr.Name);
            if (valueFromEnv == null)
            {
                throw new Exception($"Environment Variable `{attr.Name}` could not be found. The value can be found in the password store under `{attr.LastPassName}`");
            }

            return valueFromEnv;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    class EnvironmentVariableAttribute : Attribute
    {
        public string Name { get; }
        public string LastPassName { get; }
        public EnvironmentVariableAttribute(string name, string lastPassName)
        {
            Name = name;
            LastPassName = lastPassName;
        }

        public static EnvironmentVariableAttribute Get(object enm)
        {
            var mi = enm?.GetType().GetMember(enm.ToString());
            if (mi == null || mi.Length <= 0) return null;
            return GetCustomAttribute(mi[0], typeof(EnvironmentVariableAttribute)) as EnvironmentVariableAttribute;
        }
    }
}
