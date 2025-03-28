using System;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Integration.Packages.Download
{
    public interface IEcrFeedLoginDetailsProvider
    {
        Task<(string Username, string Password, string FeedUri)> GetFeedLoginDetails(
            IVariables variables,
            string username,
            string password);
    }

    public class EcrFeedLoginDetailsProvider : IEcrFeedLoginDetailsProvider
    {
        public async Task<(string Username, string Password, string FeedUri)> GetFeedLoginDetails(IVariables variables, string username, string password)
        {
            var usingOidc = !string.IsNullOrWhiteSpace(variables.Get(AuthenticationVariables.Jwt));
            if (usingOidc)
            {
                return await AwsAuthenticationProvider.GetEcrOidcCredentials(variables);
            }
            return await AwsAuthenticationProvider.GetEcrAccessKeyCredentials(variables, username, password);
        }
    }
}