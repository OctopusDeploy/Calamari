using System;
using System.Net.Http;
using System.Threading;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Rest;
using NetWebRequest = System.Net.WebRequest;
using AzureEnvironmentEnum = Microsoft.Azure.Management.ResourceManager.Fluent.AzureEnvironment;

namespace Calamari.CloudAccounts
{
    public interface IAzureClientFactory
    {
        IAzure CreateAzureClientFromAccount(IAzureAccount account);
    }

    public class AzureClientFactory : IAzureClientFactory
    {
        // to ensure the Azure API uses the appropriate web proxy
        static HttpClient HttpClient => new HttpClient(new HttpClientHandler { Proxy = NetWebRequest.DefaultWebProxy });

        public IAzure CreateAzureClientFromAccount(IAzureAccount account)
        {
            switch (account.AccountType)
            {
                case AccountType.AzureOidc:
                    return CreateAzureClientForOidcAccount(account as AzureOidcAccount);
                case AccountType.AzureServicePrincipal:
                    return CreateAzureClientForServicePrincipalAccount(account as AzureServicePrincipalAccount);
                default:
                    throw new ArgumentException($"{account.AccountType} is not supported");
            }
        }

        static IAzure CreateAzureClientForOidcAccount(AzureOidcAccount account)
        {
            var environment = string.IsNullOrEmpty(account.AzureEnvironment) || account.AzureEnvironment == "AzureCloud"
                ? AzureEnvironmentEnum.AzureGlobalCloud
                : AzureEnvironmentEnum.FromName(account.AzureEnvironment) ?? throw new InvalidOperationException($"Unknown environment name {account.AzureEnvironment}");

            var accessToken = account.GetAuthorizationToken(CancellationToken.None).GetAwaiter().GetResult();
            var credentials = new AzureCredentials(
                                                   new TokenCredentials(accessToken),
                                                   new TokenCredentials(accessToken),
                                                   account.TenantId,
                                                   environment);

            return Azure.Configure()
                        .WithHttpClient(HttpClient)
                        .Authenticate(credentials)
                        .WithSubscription(account.SubscriptionNumber);
        }

        static IAzure CreateAzureClientForServicePrincipalAccount(AzureServicePrincipalAccount account)
        {
            var environment = string.IsNullOrEmpty(account.AzureEnvironment) || account.AzureEnvironment == "AzureCloud"
                ? AzureEnvironmentEnum.AzureGlobalCloud
                : AzureEnvironmentEnum.FromName(account.AzureEnvironment) ?? throw new InvalidOperationException($"Unknown environment name {account.AzureEnvironment}");

            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(
                                                                                      account.ClientId,
                                                                                      account.GetCredentials,
                                                                                      account.TenantId,
                                                                                      environment);

            return Azure.Configure()
                        .WithHttpClient(HttpClient)
                        .Authenticate(credentials)
                        .WithSubscription(account.SubscriptionNumber);
        }
    }
}