using System;
using System.Net;
using System.Net.Http;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    public class OciArtifactManifestRetriever
    {
        public OciArtifactTypes TryGetArtifactType(string packageId,
                                                   IVersion version,
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
                var isHelmChart = //Oci.HasAnnotationContaining(jsonManifest, Oci.Manifest.Annotations.HelmChartAnnotationKey, "helm")
                                  Oci.HasConfigMediaTypeContaining(jsonManifest, Oci.Manifest.Config.OciImageMediaTypeValue)
                                  || Oci.HasLayersMediaTypeContaining(jsonManifest, Oci.Manifest.Layers.HelmChartMediaTypeValue);
                
                if (isHelmChart)
                    return OciArtifactTypes.HelmChart;

                var isDockerImage = Oci.HasMediaTypeContaining(jsonManifest, Oci.Manifest.DockerImageMediaTypeValue)
                    || Oci.HasConfigMediaTypeContaining(jsonManifest, Oci.Manifest.Config.DockerImageMediaTypeValue)
                    || Oci.HasLayersMediaTypeContaining(jsonManifest, Oci.Manifest.Layers.DockerImageMediaTypeValue);

                return isDockerImage ? OciArtifactTypes.DockerImage : OciArtifactTypes.Unknown;
            }
            catch (Exception ex)
            {
                return OciArtifactTypes.Unknown;
            }
        }
    }
}