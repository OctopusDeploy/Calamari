using System;
using System.IO;
using System.Linq;
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
        const string IndexFile = "index.yaml";
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
            var endpoint = SanitiseUrl(chartRepositoryRootUrl);

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
        
        /// <summary>
        /// The URL must not include the index.yaml path, but must include a trailing slash
        /// </summary>
        /// <param name="url">The URL to sanitise</param>
        /// <returns>A sanitised URL allowing for subpaths in the helm repo</returns>
        public static Uri SanitiseUrl(Uri url)
        {
            if (url.Segments.Last().EndsWith(IndexFile))
            {
                return url;
            }

            return new Uri($"{url.ToString().TrimEnd('/')}/{IndexFile}");
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