using System;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Integration.Packages.Download
{
    public interface IFeedLoginDetailsProvider
    {
        Task<(string Username, string Password, Uri FeedUri)> GetFeedLoginDetails(
            IVariables variables,
            string? username,
            string? password,
            Uri feedUri);
    }

    public class EcrFeedLoginDetailsProvider : IFeedLoginDetailsProvider
    {
        public async Task<(string Username, string Password, Uri FeedUri)> GetFeedLoginDetails(IVariables variables, string? username, string? password, Uri feedUri)
        {
            var usingOidc = !string.IsNullOrWhiteSpace(variables.Get(AuthenticationVariables.Jwt));
            Log.Verbose(usingOidc ? "Ecr Feed - OIDC token detected - using token-based authentication flow" : $"Ecr Feed Using username/password authentication flow. Username provided: {!string.IsNullOrEmpty(username)}");
            if (usingOidc)
            {
                return await AwsAuthenticationProvider.GetEcrOidcCredentials(variables);
            }
            return await AwsAuthenticationProvider.GetEcrAccessKeyCredentials(variables, username ?? string.Empty, password ?? string.Empty);
        }
    }
    
    
}