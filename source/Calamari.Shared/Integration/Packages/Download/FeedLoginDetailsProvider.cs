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
            var feedType = variables.Get("FeedType");

            switch (feedType)
            {
                case "AwsElasticContainerRegistry":
                    var usingOidc = !string.IsNullOrWhiteSpace(variables.Get("Jwt"));
                    if (usingOidc)
                    {
                        return AwsAuthenticationProvider.GetAwsOidcCredentials(variables).GetAwaiter().GetResult();
                    }
                    return AwsAuthenticationProvider.GetAwsAccessKeyCredentials(variables, username, password);
                default:
                    throw new NotSupportedException($"Feed type '{feedType}' not supported by FeedLoginDetailsProvider.");
            }
        }
    }
}