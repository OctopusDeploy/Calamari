using System;
using Azure.Identity;
using Azure.ResourceManager;

namespace Calamari.Azure
{
    public sealed class AzureKnownEnvironment
    {
        /// <param name="environment">The environment name exactly matching the names defined in Azure SDK (see here https://github.com/Azure/azure-libraries-for-net/blob/master/src/ResourceManagement/ResourceManager/AzureEnvironment.cs)
        /// Other names are allowed in case this list is ever expanded/changed, but will likely result in an error at deployment time.
        /// </param>
        public AzureKnownEnvironment(string environment)
        {
            Value = environment;

            if (string.IsNullOrEmpty(environment) || environment == "AzureCloud") // This environment name is defined in Sashimi.Azure.Accounts.AzureEnvironmentsListAction
                Value = Global.Value;                                             // We interpret it as the normal Azure environment for historical reasons)

            // Validate that the environment name is known by attempting to resolve it
            ToArmEnvironment(Value);
        }

        public string Value { get; }

        public static readonly AzureKnownEnvironment Global = new AzureKnownEnvironment("AzureGlobalCloud");
        public static readonly AzureKnownEnvironment AzureChinaCloud = new AzureKnownEnvironment("AzureChinaCloud");
        public static readonly AzureKnownEnvironment AzureUSGovernment = new AzureKnownEnvironment("AzureUSGovernment");
        public static readonly AzureKnownEnvironment AzureGermanCloud = new AzureKnownEnvironment("AzureGermanCloud");

        public ArmEnvironment AsAzureArmEnvironment() => ToArmEnvironment(Value);

        private static ArmEnvironment ToArmEnvironment(string name) => name switch
        {
            "AzureGlobalCloud" => ArmEnvironment.AzurePublicCloud,
            "AzureChinaCloud" => ArmEnvironment.AzureChina,
            "AzureGermanCloud" => ArmEnvironment.AzureGermany,
            "AzureUSGovernment" => ArmEnvironment.AzureGovernment,
            _ => throw new InvalidOperationException($"Unknown environment name {name}")
        };

        public Uri GetAzureAuthorityHost() => ToAzureAuthorityHost(Value);

        private static Uri ToAzureAuthorityHost(string name) => name switch
        {
            "AzureGlobalCloud" => AzureAuthorityHosts.AzurePublicCloud,
            "AzureChinaCloud" => AzureAuthorityHosts.AzureChina,
#pragma warning disable CS0618 // Type or member is obsolete - Microsoft Cloud Germany was closed on October 29th, 2021.
            "AzureGermanCloud" => AzureAuthorityHosts.AzureGermany,
#pragma warning restore CS0618 // Type or member is obsolete
            "AzureUSGovernment" => AzureAuthorityHosts.AzureGovernment,
            _ => throw new InvalidOperationException($"Unknown environment name {name}")
        };
    }
}