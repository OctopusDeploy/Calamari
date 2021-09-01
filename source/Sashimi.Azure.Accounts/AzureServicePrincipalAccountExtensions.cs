using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure.Management.WebSites;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;

namespace Sashimi.Azure.Accounts
{
    static class AzureServicePrincipalAccountExtensions
    {
        static ServiceClientCredentials Credentials(this AzureServicePrincipalAccountDetails account, HttpMessageHandler handler)
        {
            return new TokenCredentials(GetAuthorizationToken(account, handler));
        }

        public static ResourceManagementClient CreateResourceManagementClient(this AzureServicePrincipalAccountDetails account, HttpClientHandler httpClientHandler)
        {
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ? new ResourceManagementClient(account.Credentials(httpClientHandler), httpClientHandler) { SubscriptionId = account.SubscriptionNumber } : new ResourceManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), account.Credentials(httpClientHandler), httpClientHandler) { SubscriptionId = account.SubscriptionNumber };
        }

        public static StorageManagementClient CreateStorageManagementClient(this AzureServicePrincipalAccountDetails account, HttpClientHandler httpClientHandler)
        {
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ? new StorageManagementClient(account.Credentials(httpClientHandler), httpClientHandler) { SubscriptionId = account.SubscriptionNumber } : new StorageManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), account.Credentials(httpClientHandler), httpClientHandler) { SubscriptionId = account.SubscriptionNumber };
        }

        public static WebSiteManagementClient CreateWebSiteManagementClient(this AzureServicePrincipalAccountDetails account, HttpClientHandler httpClientHandler)
        {
            return string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri) ? new WebSiteManagementClient(account.Credentials(httpClientHandler), httpClientHandler) { SubscriptionId = account.SubscriptionNumber } : new WebSiteManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), account.Credentials(httpClientHandler), httpClientHandler) { SubscriptionId = account.SubscriptionNumber };
        }

        static string GetAuthorizationToken(AzureServicePrincipalAccountDetails account, HttpMessageHandler handler)
        {
            var context = new AuthenticationContext(account.Authority, true, TokenCache.DefaultShared, new HttpClientFactory(handler));

            var resourceManagementEndpointBaseUri = "https://management.core.windows.net/";
            if (!string.IsNullOrWhiteSpace(account.ResourceManagementEndpointBaseUri))
                resourceManagementEndpointBaseUri = account.ResourceManagementEndpointBaseUri;

            var result = context.AcquireTokenAsync(resourceManagementEndpointBaseUri, new ClientCredential(account.ClientId, account.Password?.Value)).GetAwaiter().GetResult();
            return result.AccessToken;
        }

        public static void InvalidateTokenCache(this AzureServicePrincipalAccountDetails account, HttpClientHandler httpClientHandler)
        {
            var context = new AuthenticationContext(account.Authority, true, TokenCache.DefaultShared, new HttpClientFactory(httpClientHandler));

            var cachedToken = context.TokenCache.ReadItems().FirstOrDefault(z => z.ClientId == account.ClientId);
            if (cachedToken != null) context.TokenCache.DeleteItem(cachedToken);
        }

        // We decided to "copy" the impl from https://github.com/AzureAD/azure-activedirectory-library-for-dotnet/blob/af10dd15cdb082bc3dbe14b0c2c6d81f6ca5b541/src/Microsoft.IdentityModel.Clients.ActiveDirectory/Core/Http/HttpClientFactory.cs
        // This was we are setting the same headers as the Azure impl.
        class HttpClientFactory : IHttpClientFactory
        {
            const long MaxResponseContentBufferSizeInBytes = 1048576;
            readonly HttpClient client;

            public HttpClientFactory(HttpMessageHandler handler)
            {
                client = new HttpClient(handler)
                {
                    MaxResponseContentBufferSize = MaxResponseContentBufferSizeInBytes
                };
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            public HttpClient GetHttpClient()
            {
                return client;
            }
        }
    }
}