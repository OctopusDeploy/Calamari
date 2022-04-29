using Calamari.CloudAccounts.Azure;

namespace Calamari.CloudAccounts
{
    /// <remarks>
    /// This class is being kept to maintain backwards compatibility
    /// as it's a public interface for the Calamari.CloudAccounts Nuget package. - Scott M 29/04/22
    /// </remarks>
    public static class AzureAccountVariables
    {
        public static readonly string Environment = AccountVariables.Environment;
        public static readonly string AccountVariable = "Octopus.Action.AzureAccount.Variable";
        public static readonly string SubscriptionId = AccountVariables.SubscriptionId;
        public static readonly string ClientId = AccountVariables.ClientId;   
        public static readonly string TenantId = AccountVariables.TenantId;
        public static readonly string Password = AccountVariables.Password;
        public static readonly string ResourceManagementEndPoint = AccountVariables.ResourceManagementEndPoint;
        public static readonly string ActiveDirectoryEndPoint = AccountVariables.ActiveDirectoryEndPoint;
    }
}