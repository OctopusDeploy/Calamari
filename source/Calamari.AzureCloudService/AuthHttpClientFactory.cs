using System;
using System.Net;
using System.Net.Http;

namespace Calamari.AzureCloudService
{
    public class AuthHttpClientFactory
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