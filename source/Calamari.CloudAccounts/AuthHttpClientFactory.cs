using System.Net;
using System.Net.Http;
using Calamari.Common.Plumbing.Logging;
using Microsoft.Identity.Client;

namespace Calamari.CloudAccounts
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
            Log.Verbose($"Proxy Is Set As {WebRequest.DefaultWebProxy}");
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
