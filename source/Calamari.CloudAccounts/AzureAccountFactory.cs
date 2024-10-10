using Calamari.Common.Plumbing.Variables;

namespace Calamari.CloudAccounts
{
    public static class AzureAccountFactory
    {
        public static IAzureAccount Create(IVariables variables)
            => !string.IsNullOrEmpty(variables.Get(AzureAccountVariables.OpenIDJwt)) //if we have an OpenIdConnect JWT, return an AzureOidcAccount
                ? (IAzureAccount)new AzureOidcAccount(variables)
                : new AzureServicePrincipalAccount(variables);
    }
}