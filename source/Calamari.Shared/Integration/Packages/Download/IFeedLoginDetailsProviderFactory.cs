using System;
using Calamari.Common.Features.Packages;

namespace Calamari.Integration.Packages.Download
{
    public interface IFeedLoginDetailsProviderFactory
    {
        IFeedLoginDetailsProvider GetFeedLoginDetailsProvider(FeedType feedType);
    }
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
                default:
                    throw new NotImplementedException($"No login provider implementation for feed type `{feedType}`.");
            }
        }
    }
}