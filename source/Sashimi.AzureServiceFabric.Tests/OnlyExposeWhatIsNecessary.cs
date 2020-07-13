using System;
using System.Collections.Generic;
using NUnit.Framework;
using Octopus.Server.Extensibility.Tests;
using Sashimi.AzureServiceFabric.Endpoints;

namespace Sashimi.AzureServiceFabric.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary : OnlyExposeWhatIsNecessaryFixture
    {
        protected override Type EntryPointTypeUnderTest => typeof(AzureServiceFabricModule);
        
        protected override IEnumerable<Type> KnownClassesWhoAreBendingTheRules
        {
            get
            {
                yield return typeof(AzureServiceFabricCredentialType);
                yield return typeof(AzureServiceFabricSecurityMode);
                yield return typeof(AzureServiceFabricClusterEndpoint);
                yield return typeof(ServiceFabricEndpointResource);
            }
        }
    }
}