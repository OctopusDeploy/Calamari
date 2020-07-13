using System;
using System.Collections.Generic;
using NUnit.Framework;
using Octopus.Server.Extensibility.Tests;
using Sashimi.AzureCloudService.Endpoints;

namespace Sashimi.AzureCloudService.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary : OnlyExposeWhatIsNecessaryFixture
    {
        protected override Type EntryPointTypeUnderTest => typeof(AzureCloudServiceModule);

        protected override IEnumerable<Type> KnownClassesWhoAreBendingTheRules
        {
            get
            {
                yield return typeof(AccountTypes);
                yield return typeof(AzureSubscriptionDetails);
                yield return typeof(AzureCloudServiceEndpoint);
                yield return typeof(CloudServiceEndpointResource);
            }
        }
    }
}