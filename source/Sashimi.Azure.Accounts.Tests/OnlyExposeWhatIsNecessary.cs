using System;
using System.Collections.Generic;
using NUnit.Framework;
using Octopus.Server.Extensibility.Tests;

namespace Sashimi.Azure.Accounts.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary : OnlyExposeWhatIsNecessaryFixture
    {
        protected override Type EntryPointTypeUnderTest => typeof(AzureAccountModule);

        protected override IEnumerable<Type> KnownClassesWhoAreBendingTheRules
        {
            get { yield return typeof(AccountTypes); }
        }
    }
}