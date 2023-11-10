using System.Net;
using System.Net.Http;
using Microsoft.Identity.Client;

namespace Calamari.AzureAppService
{
    public class AuthHttpClientFactory : IMsalHttpClientFactory
    {
        static readonly HttpClient _httpClient;

        static AuthHttpClientFactory()
        {
            var proxyHttpClientHandler = ProxyClientHandler();

            _httpClient = new HttpClient(proxyHttpClientHandler);
        }

        public static HttpClientHandler ProxyClientHandler()
        {
            return new HttpClientHandler
            {
                Proxy = WebRequest.DefaultWebProxy,
                UseProxy = true,
            };
        }

        public HttpClient GetHttpClient()
        {
            return _httpClient;
        }
    }
}
