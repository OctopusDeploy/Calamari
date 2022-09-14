using System.Net;
using System.Net.Http;

namespace Calamari.AzureAppService.Azure.Rest
{
    public class AzureRestClientException : HttpRequestException
    {
        public HttpResponseMessage Response { get; }

        public AzureRestClientException(HttpResponseMessage response) : base(
            $"Response status code does not indicate success: {response.StatusCode} ({response.ReasonPhrase})", null,
            response.StatusCode)
        {
            Response = response;
        }
    }
}