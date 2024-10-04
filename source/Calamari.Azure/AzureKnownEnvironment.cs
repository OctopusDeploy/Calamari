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
            {
                Value = "AzureGlobalCloud"; // We interpret it as the normal Azure environment for historical reasons)
            }
        }

        public string Value { get; }

        public ArmEnvironment AsAzureArmEnvironment() => ToArmEnvironment(Value);

        static ArmEnvironment ToArmEnvironment(string name) => name switch
                                                               {
                                                                   "AzureGlobalCloud" => ArmEnvironment.AzurePublicCloud,
                                                                   "AzureChinaCloud" => ArmEnvironment.AzureChina,
                                                                   "AzureGermanCloud" => ArmEnvironment.AzureGermany,
                                                                   "AzureUSGovernment" => ArmEnvironment.AzureGovernment,
                                                                   _ => throw new InvalidOperationException($"ARM Environment {name} is not a known Azure Environment name.")
                                                               };
        
        public Uri GetAzureAuthorityHost() => ToAzureAuthorityHost(Value);

        static Uri ToAzureAuthorityHost(string name) => name switch
                                                        {
                                                            "AzureGlobalCloud" => AzureAuthorityHosts.AzurePublicCloud,
                                                            "AzureChinaCloud" => AzureAuthorityHosts.AzureChina,
                                                            "AzureGermanCloud" => AzureAuthorityHosts.AzureGermany,
                                                            "AzureUSGovernment" => AzureAuthorityHosts.AzureGovernment,
                                                            _ => throw new InvalidOperationException($"ARM Environment {name} is not a known Azure Environment name.")
                                                        };
    }
}