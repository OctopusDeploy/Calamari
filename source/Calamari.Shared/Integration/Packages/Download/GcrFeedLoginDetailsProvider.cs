using System;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Integration.Packages.Download
{
    public class GcrFeedLoginDetailsProvider : IFeedLoginDetailsProvider
    {
        public async Task<(string Username, string Password, Uri FeedUri)> GetFeedLoginDetails(IVariables variables, string? username, string? password, Uri feedUri)
        { 
            var gcrAuth = new GoogleAuthenticationProvider();
            var usingOidc = !string.IsNullOrWhiteSpace(variables.Get(AuthenticationVariables.Jwt));
            Log.Verbose(usingOidc ? "Gcr Feed - OIDC token detected - using token-based authentication flow." : "Gcr Feed - Using json key authentication flow.");
            return usingOidc ? (await gcrAuth.GetGcrOidcCredentials(variables, feedUri)) : ( await Task.FromResult(gcrAuth.GetGcrUserNamePasswordCredentials(username, password, feedUri)));
        }
    }
}