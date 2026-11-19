using System;
using Calamari.Aws.Packages;
using Calamari.Azure.Packages;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.Acquisition;
using Calamari.GoogleCloud.Packages;
using Calamari.Integration.Packages.Download;

namespace Calamari.Commands.Support;

public class FeedLoginDetailsProviderFactory: IFeedLoginDetailsProviderFactory
{
    public IFeedLoginDetailsProvider GetFeedLoginDetailsProvider(FeedType feedType)
    {
        switch (feedType)
        {
               
            case FeedType.AwsElasticContainerRegistry:
                return new EcrFeedLoginDetailsProvider();
            case FeedType.AzureContainerRegistry:
                return new AcrFeedLoginDetailsProvider();
            case FeedType.GoogleContainerRegistry:
                return new GcrFeedLoginDetailsProvider();
            default:
                throw new NotImplementedException($"No login provider implementation for feed type `{feedType}`.");
        }
    }
}