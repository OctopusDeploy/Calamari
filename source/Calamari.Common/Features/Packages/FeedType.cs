using System;

namespace Calamari.Common.Features.Packages
{
    public enum FeedType
    {
        None = 0,
        NuGet,
        Docker,
        Maven,
        GitHub,
        Helm,
        OciRegistry,
        AwsElasticContainerRegistry,
        S3,
        AzureContainerRegistry,
        GoogleContainerRegistry,
        ArtifactoryGenericFeed,
    }
}