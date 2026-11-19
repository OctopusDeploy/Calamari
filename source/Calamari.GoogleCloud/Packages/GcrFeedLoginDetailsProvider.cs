using System;
using Calamari.CloudAccounts.GoogleCloud;
using Calamari.Common.Features.Packages.Acquisition;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.GoogleCloud.Packages
{
    public class GcrFeedLoginDetailsProvider : IFeedLoginDetailsProvider
    {
        public async Task<(string Username, string Password, Uri FeedUri)> GetFeedLoginDetails(IVariables variables, string? username, string? password, Uri feedUri)
        { 
            var gcrAuth = new GoogleAuthenticationProvider();
            var usingOidc = !string.IsNullOrWhiteSpace(variables.Get(AuthenticationVariables.Jwt));
            if (usingOidc) {
                Log.Verbose("Gcr Feed - OIDC token detected - using token-based authentication flow.");
                return await gcrAuth.GetGcrOidcCredentials(variables, feedUri);
            } 
            
            Log.Verbose("Gcr Feed - Using json key authentication flow.");
            return await Task.FromResult(gcrAuth.GetGcrUserNamePasswordCredentials(username, password, feedUri));
        }
    }
}