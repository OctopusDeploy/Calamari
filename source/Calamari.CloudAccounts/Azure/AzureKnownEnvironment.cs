using System;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Calamari.CloudAccounts.Azure
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

            azureEnvironment = AzureEnvironment.FromName(Value) ??
                               throw new InvalidOperationException($"Unknown environment name {Value}");
        }

        private readonly AzureEnvironment azureEnvironment;
        public string Value { get; }

        public static readonly AzureKnownEnvironment Global = new AzureKnownEnvironment("AzureGlobalCloud");
        public static readonly AzureKnownEnvironment AzureChinaCloud = new AzureKnownEnvironment("AzureChinaCloud");
        public static readonly AzureKnownEnvironment AzureUSGovernment = new AzureKnownEnvironment("AzureUSGovernment");
        public static readonly AzureKnownEnvironment AzureGermanCloud = new AzureKnownEnvironment("AzureGermanCloud");

        public AzureEnvironment AsAzureSDKEnvironment()
        {
            return azureEnvironment;
        }
    }
}
