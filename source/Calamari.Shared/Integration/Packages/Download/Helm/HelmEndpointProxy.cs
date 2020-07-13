using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using Calamari.Common.Plumbing.Extensions;
using YamlDotNet.RepresentationModel;

namespace Calamari.Integration.Packages.Download.Helm
{
    public interface IHelmEndpointProxy
    {
        YamlStream Get(Uri chartRepositoryRootUrl, string username, string password, CancellationToken cancellationToken);
    }
    
     public class HelmEndpointProxy: IHelmEndpointProxy
    {
        static readonly string[] AcceptedContentType = {"application/x-yaml", "application/yaml"};
        static string httpAccept = string.Join( ", ", AcceptedContentType);
        
        readonly HttpClient client;

        public HelmEndpointProxy(HttpClient client)
        {
            this.client = client;
        }

        public YamlStream Get(Uri chartRepositoryRootUrl, string username, string password, CancellationToken cancellationToken)
        {
            using (var response = GetIndexYaml(chartRepositoryRootUrl, username, password, cancellationToken))
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

        HttpResponseMessage GetIndexYaml(Uri chartRepositoryRootUrl, string username, string password, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            var endpoint = new Uri(chartRepositoryRootUrl, "index.yaml");

            using (var msg = new HttpRequestMessage(HttpMethod.Get, endpoint))
            {
                ApplyAuthorization(username, password, msg);
                ApplyAccept(msg);
                response = client.SendAsync(msg, cancellationToken).Result;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unable to read Helm index file at {endpoint}.\r\n\tStatus Code: {response.StatusCode}\r\n\tResponse: {response.Content.ReadAsStringAsync().Result}");
            }

            return response;
        }
        
        void ApplyAuthorization(string username, string password, HttpRequestMessage msg)
        {
            msg.Headers.AddAuthenticationHeader(username, password);
        }

        void ApplyAccept(HttpRequestMessage msg)
        {
            msg.Headers.Add("Accept", httpAccept);
        }
    }
}