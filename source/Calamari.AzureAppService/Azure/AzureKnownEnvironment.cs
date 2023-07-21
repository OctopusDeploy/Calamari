using System;
using Azure.Identity;
using Azure.ResourceManager;

namespace Calamari.AzureAppService.Azure
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
                Value = Public.Value;                                             // We interpret it as the normal Azure environment for historical reasons)

            azureSdkEnvironment = ToArmEnvironment(Value);
        }

        private readonly ArmEnvironment azureSdkEnvironment;
        public string Value { get; }

        public static readonly AzureKnownEnvironment Public = new AzureKnownEnvironment("AzurePublicCloud");
        public static readonly AzureKnownEnvironment AzureChinaCloud = new AzureKnownEnvironment("AzureChinaCloud");
        public static readonly AzureKnownEnvironment AzureUSGovernment = new AzureKnownEnvironment("AzureUSGovernment");
        public static readonly AzureKnownEnvironment AzureGermanCloud = new AzureKnownEnvironment("AzureGermanCloud");

        public ArmEnvironment AsArmEnvironment()
        {
            return azureSdkEnvironment;
        }

        public ArmEnvironment AsAzureArmEnvironment() => ToArmEnvironment(Value);

        private static ArmEnvironment ToArmEnvironment(string name) => name switch
        {
            "AzureGlobalCloud" => ArmEnvironment.AzurePublicCloud,
            "AzurePublicCloud" => ArmEnvironment.AzurePublicCloud,
            "AzureChinaCloud" => ArmEnvironment.AzureChina,
            "AzureGermanCloud" => ArmEnvironment.AzureGermany,
            "AzureUSGovernment" => ArmEnvironment.AzureGovernment,
            _ => throw  new InvalidOperationException($"ARM Environment {name} is not a known Azure Environment name.")
        };

        public Uri GetAzureAuthorityHost() => ToAzureAuthorityHost(Value);

        private static Uri ToAzureAuthorityHost(string name) => name switch
        {
            "AzureGlobalCloud" => AzureAuthorityHosts.AzurePublicCloud,
            "AzurePublicCloud" => AzureAuthorityHosts.AzurePublicCloud,
            "AzureChinaCloud" => AzureAuthorityHosts.AzureChina,
            "AzureGermanCloud" => AzureAuthorityHosts.AzureGermany,
            "AzureUSGovernment" => AzureAuthorityHosts.AzureGovernment,
            _ => throw new InvalidOperationException($"ARM Environment {name} is not a known Azure Environment name.")
        };
    }
}
