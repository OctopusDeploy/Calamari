using System;
using System.Collections.Generic;
using NUnit.Framework;
using Octopus.Server.Extensibility.Tests;
using Sashimi.Azure.Accounts;

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
            }
        }
    }
}