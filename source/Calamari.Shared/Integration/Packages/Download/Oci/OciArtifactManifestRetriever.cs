using System;
using Calamari.Common.Plumbing.Logging;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download.Oci
{
    public class OciArtifactManifestRetriever
    {
        readonly OciClient ociClient;
        readonly ILog log;

        public OciArtifactManifestRetriever(OciClient ociClient, ILog log)
        {
            this.ociClient = ociClient;
            this.log = log;
        }

        public OciArtifactTypes TryGetArtifactType(
            string packageId,
            IVersion version,
            Uri feedUri,
            string? feedUsername,
            string? feedPassword)
        {
            try
            {
                var jsonManifest = ociClient.GetManifest(feedUri, packageId, version, feedUsername, feedPassword);

                // Check for Helm chart annotations
                var isHelmChart =
                    jsonManifest.HasConfigMediaTypeContaining(OciConstants.Manifest.Config.OciImageMediaTypeValue)
                    || jsonManifest.HasLayersMediaTypeContaining(OciConstants.Manifest.Layers.HelmChartMediaTypeValue);

                if (isHelmChart)
                    return OciArtifactTypes.HelmChart;

                var isDockerImage = jsonManifest.HasMediaTypeContaining(OciConstants.Manifest.DockerImageMediaTypeValue)
                                    || jsonManifest.HasConfigMediaTypeContaining(OciConstants.Manifest.Config.DockerImageMediaTypeValue)
                                    || jsonManifest.HasLayersMediaTypeContaining(OciConstants.Manifest.Layers.DockerImageMediaTypeValue);

                return isDockerImage ? OciArtifactTypes.DockerImage : OciArtifactTypes.Unknown;
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to get artifact type: {Message}", ex.Message);
                return OciArtifactTypes.Unknown;
            }
        }
    }
}