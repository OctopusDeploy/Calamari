using System.Net;
using System.Net.Http;
using Microsoft.Identity.Client;

namespace Calamari.CloudAccounts
{
    public class AuthHttpClientFactory : IMsalHttpClientFactory
    {
        static readonly HttpClient _httpClient;

        static AuthHttpClientFactory()
        {
            var proxyHttpClientHandler = new HttpClientHandler
            {
                Proxy = WebRequest.DefaultWebProxy,
                UseProxy = true,
            };

            _httpClient = new HttpClient(proxyHttpClientHandler);
        }

        public HttpClient GetHttpClient()
        {
            return _httpClient;
        }
    }
}
