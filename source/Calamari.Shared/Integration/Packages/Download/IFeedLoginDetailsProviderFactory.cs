using System;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.Acquisition;

namespace Calamari.Integration.Packages.Download
{
    public interface IFeedLoginDetailsProviderFactory
    {
        IFeedLoginDetailsProvider GetFeedLoginDetailsProvider(FeedType feedType);
    }
}