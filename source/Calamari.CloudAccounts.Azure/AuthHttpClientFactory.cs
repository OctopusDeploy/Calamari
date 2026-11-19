using System;
using System.Net;
using Microsoft.Identity.Client;

namespace Calamari.CloudAccounts.Azure
{
    public class AuthHttpClientFactory : IMsalHttpClientFactory
    {
        static readonly HttpClient HttpClient;

        static AuthHttpClientFactory()
        {
            var proxyHttpClientHandler = ProxyClientHandler();

            HttpClient = new HttpClient(proxyHttpClientHandler);
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
            return HttpClient;
        }
    }
}
