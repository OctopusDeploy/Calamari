using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{

    public class OciArtifactManifestRetriever
    {
        public OciArtifactTypes TryGetArtifactType(string packageId,
                                                   IVersion version,
                                                   string feedId,
                                                   Uri feedUri,
                                                   string? feedUsername,
                                                   string? feedPassword)
        {
            try
            {
                var versionString = Oci.FixVersion(version);
                var apiUrl = Oci.GetApiUri(feedUri);

                var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None });

                var jsonManifest = Oci.GetManifest(client,
                                                   apiUrl,
                                                   packageId,
                                                   versionString,
                                                   feedUsername,
                                                   feedPassword);

                // Check for Helm chart annotations
                var annotations = jsonManifest["annotations"];
                if (annotations != null && annotations.Type == JTokenType.Object)
                {
                    var chartAnnotation = annotations["io.helm.sh/chart"];
                    if (chartAnnotation != null)
                    {
                        return OciArtifactTypes.HelmChart;
                    }
                }

                // Check the media type in the config section for Docker images and other OCI artifacts
                var config = jsonManifest["config"];
                if (config != null && config["mediaType"] != null)
                {
                    var mediaType = config["mediaType"].ToString();
                    if (mediaType.Contains("docker"))
                    {
                        return OciArtifactTypes.DockerImage;
                    }
                }

                return OciArtifactTypes.Unknown;
            }
            catch
            {
                return OciArtifactTypes.Unknown;
            }
        }

        public bool HasAnnotationContaining(JObject manifest, string name)
        {
            var annotations = manifest["annotations"];
            return annotations is { Type: JTokenType.Object }
                   && annotations[name] != null;
        }

        public bool HasConfigMediaTypeContaining(JObject manifest, string name)
        {
            var config = manifest["config"];

            return config is { Type: JTokenType.Object }
                   && config["mediaType"] != null
                   && config["mediaType"].ToString().Contains(name);
        }

        public bool HasLayersMediaTypeContaining(JObject manifest, string name)
        {
            var layers = manifest["layers"];

            if (layers is { Type: JTokenType.Array })
            {
                foreach (var layer in layers)
                {
                    if (layer["mediaType"] != null && layer["mediaType"].ToString().Contains(name))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

    }
}