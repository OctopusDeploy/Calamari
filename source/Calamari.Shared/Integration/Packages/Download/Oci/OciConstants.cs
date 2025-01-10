using System;

namespace Calamari.Integration.Packages.Download.Oci
{
    public static class OciConstants
    {
        public class Manifest
        {
            internal const string MediaTypePropertyName = "mediaType";
            internal const string DockerImageMediaTypeValue = "application/vnd.docker.distribution.manifest.v2+json";
            internal const string AcceptHeader = "application/vnd.oci.image.manifest.v1+json";

            public class Config
            {
                internal const string PropertyName = "config";
                internal const string MediaTypePropertyName = "mediaType";
                internal const string OciImageMediaTypeValue = "application/vnd.oci.image.config.v1+json";
                internal const string DockerImageMediaTypeValue = "application/vnd.docker.container.image.v1+json";
            }

            public class Image
            {
                internal const string TitleAnnotationKey = "org.opencontainers.image.title";
            }

            public class Layers
            {
                internal const string PropertyName = "layers";
                internal const string DigestPropertyName = "digest";
                internal const string SizePropertyName = "size";
                internal const string MediaTypePropertyName = "mediaType";
                internal const string AnnotationsPropertyName = "annotations";
                internal const string HelmChartMediaTypeValue = "application/vnd.cncf.helm.chart.content.v1.tar+gzip"; // https://helm.sh/docs/topics/registries/#oci-feature-deprecation-and-behavior-changes-with-v370
                internal const string DockerImageMediaTypeValue = "application/vnd.docker.image.rootfs.diff.tar.gzip";
            }
        }
    }
}