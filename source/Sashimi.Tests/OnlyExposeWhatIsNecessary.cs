using System;
using System.Collections.Generic;
using NUnit.Framework;
using Octopus.Server.Extensibility.Tests;
using Sashimi.Azure.Accounts;
using Sashimi.AzureWebApp.Endpoints;

namespace Sashimi.AzureWebApp.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary : OnlyExposeWhatIsNecessaryFixture
    {
        protected override Type EntryPointTypeUnderTest => typeof(AzureWebAppModule);

        protected override IEnumerable<Type> KnownClassesWhoAreBendingTheRules
        {
            get
            {
                yield return typeof(AccountTypes);
                yield return typeof(AzureWebAppEndpoint);
                yield return typeof(AzureWebAppEndpointResource);
            }
        }
    }
}