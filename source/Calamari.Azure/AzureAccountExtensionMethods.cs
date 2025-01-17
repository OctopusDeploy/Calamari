using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Azure
{
    public static class AzureAccountExtensionMethods
    {
        public static bool HasAzureAccountJwt(this IVariables variables)
        {
            return !string.IsNullOrEmpty(variables.Get(AzureAccountVariables.OpenIDJwt));
        }
    }
}