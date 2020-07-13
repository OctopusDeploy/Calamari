using System;
using System.Net.Http.Headers;
using System.Text;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class HttpClientExtensions
    {
        public static void AddAuthenticationHeader(this HttpRequestHeaders headers, string userName, string password)
        {
            if (!string.IsNullOrWhiteSpace(userName))
            {
                var byteArray = Encoding.ASCII.GetBytes($"{userName}:{password}");
                headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }
            else if (!string.IsNullOrWhiteSpace(password))
            {
                headers.Authorization = new AuthenticationHeaderValue("Token", password);
            }
        }
    }
}