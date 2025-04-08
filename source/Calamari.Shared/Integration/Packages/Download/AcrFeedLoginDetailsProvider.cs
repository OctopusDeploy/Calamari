using System;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Integration.Packages.Download
{
    public class AcrFeedLoginDetailsProvider: IFeedLoginDetailsProvider
    {
        public async Task<(string Username, string Password, string FeedUri)> GetFeedLoginDetails(IVariables variables, string? username, string? password)
        { 
            var arcAuth = new AzureContainerRegistryAuthenticationProvider();
            var usingOidc = !string.IsNullOrWhiteSpace(variables.Get(AuthenticationVariables.Jwt));
            Log.Verbose(usingOidc ? "OIDC token detected - using token-based authentication flow" : $"Using username/password authentication flow. Username provided: {!string.IsNullOrEmpty(username)}");
            return usingOidc ? (await arcAuth.GetAcrOidcCredentials(variables)) : ( await Task.FromResult(arcAuth.GetAcrUserNamePasswordCredentials(username, password, variables)));
        }
    }
}