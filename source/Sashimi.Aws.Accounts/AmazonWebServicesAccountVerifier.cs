using System;
using System.Net.Http;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Aws.Accounts
{
    class AmazonWebServicesAccountVerifier : IVerifyAccount
    {
        readonly AmazonSecurityTokenServiceConfig tokenServiceConfig;

        public AmazonWebServicesAccountVerifier(AwsHttpClientFactory awsHttpClientFactory)
        {
            tokenServiceConfig = new AmazonSecurityTokenServiceConfig
            {
                HttpClientFactory = awsHttpClientFactory
            };
        }

        public void Verify(AccountDetails account)
        {
            var accountTyped = (AmazonWebServicesAccountDetails)account;
            using (var client = new AmazonSecurityTokenServiceClient(new BasicAWSCredentials(accountTyped.AccessKey, accountTyped.SecretKey?.Value), tokenServiceConfig))
            {
                client.GetCallerIdentityAsync(new GetCallerIdentityRequest()).Wait();
            }
        }
    }

    class AwsHttpClientFactory : HttpClientFactory
    {
        readonly Lazy<IOctopusHttpClientFactory> httpClientFactory;

        public AwsHttpClientFactory(Lazy<IOctopusHttpClientFactory> httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        {
            return httpClientFactory.Value.CreateClient();
        }
    }
}