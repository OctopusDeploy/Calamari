using System;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Integration.Packages.Download
{
    public interface IFeedLoginDetailsProvider
    {
        (string Username, string Password, string FeedUri) GetFeedLoginDetails(
            IVariables variables,
            string username,
            string password);
    }

    public class FeedLoginDetailsProvider : IFeedLoginDetailsProvider
    {
        public (string Username, string Password, string FeedUri) GetFeedLoginDetails(IVariables variables, string username, string password)
        {
            var feedType = variables.Get(AuthenticationVariables.FeedType);

            switch (feedType)
            {
                case "AwsElasticContainerRegistry":
                    var usingOidc = !string.IsNullOrWhiteSpace(variables.Get(AuthenticationVariables.Jwt));
                    if (usingOidc)
                    {
                        return AwsAuthenticationProvider.GetEcrOidcCredentials(variables).GetAwaiter().GetResult();
                    }
                    return AwsAuthenticationProvider.GetEcrAccessKeyCredentials(variables, username, password);
                default:
                    throw new NotSupportedException($"Feed type '{feedType}' not supported by FeedLoginDetailsProvider.");
            }
        }
    }
}