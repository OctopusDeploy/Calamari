using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using Calamari.Extensions;
using YamlDotNet.RepresentationModel;

namespace Calamari.Integration.Packages.Download.Helm
{
    public interface IHelmEndpointProxy
    {
        YamlStream Get(CancellationToken cancellationToken);
    }
    
     public class HelmEndpointProxy: IHelmEndpointProxy
    {
        readonly Uri endpoint;
        readonly string username;
        readonly string password;
        static readonly string[] AcceptedContentType = {"application/x-yaml", "application/yaml"};
        static string httpAccept = string.Join( ", ", AcceptedContentType);
        
        readonly HttpClient client;

        public HelmEndpointProxy(HttpClient client, Uri endpoint, string username, string password)
        {
            this.endpoint = new Uri(endpoint, "index.yaml");
            this.username = username;
            this.password = password;
            this.client = client;
        }

        public YamlStream Get(CancellationToken cancellationToken)
        {
            using (var response = PerformRequest(cancellationToken))
            {
                var stream = response.Content.ReadAsStreamAsync().Result;

                using (var sr = new StreamReader(stream))
                {
                    var yaml = new YamlStream();
                    yaml.Load(sr);
                    return yaml;
                }
            }
        }

        HttpResponseMessage PerformRequest(CancellationToken cancellationToken)
        {
            HttpResponseMessage response;

            using (var msg = new HttpRequestMessage(HttpMethod.Get, endpoint))
            {
                ApplyAuthorization(msg);
                ApplyAccept(msg);
                response = client.SendAsync(msg, cancellationToken).Result;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unable to read Helm index file at {endpoint}.\r\n\tStatus Code: {response.StatusCode}\r\n\tResponse: {response.Content.ReadAsStringAsync().Result}");
            }

            return response;
        }
        
        void ApplyAuthorization(HttpRequestMessage msg)
        {
            msg.Headers.AddAuthenticationHeader(username, password);
        }

        void ApplyAccept(HttpRequestMessage msg)
        {
            msg.Headers.Add("Accept", httpAccept);
        }
    }
}