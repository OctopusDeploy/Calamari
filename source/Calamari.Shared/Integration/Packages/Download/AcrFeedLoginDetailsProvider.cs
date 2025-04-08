using System;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Integration.Packages.Download
{
    public class AcrFeedLoginDetailsProvider: IFeedLoginDetailsProvider
    {
        public async Task<(string Username, string Password, Uri FeedUri)> GetFeedLoginDetails(IVariables variables, string? username, string? password, Uri feedUri)
        { 
            var arcAuth = new AzureContainerRegistryAuthenticationProvider();
            var usingOidc = !string.IsNullOrWhiteSpace(variables.Get(AuthenticationVariables.Jwt));
            Log.Verbose(usingOidc ? "Acr Feed - OIDC token detected - using token-based authentication flow" : $"Acr Feed - Using username/password authentication flow. Username provided: {!string.IsNullOrEmpty(username)}");
            return usingOidc ? (await arcAuth.GetAcrOidcCredentials(variables, feedUri)) : ( await Task.FromResult(arcAuth.GetAcrUserNamePasswordCredentials(username, password, feedUri)));
        }
    }
}