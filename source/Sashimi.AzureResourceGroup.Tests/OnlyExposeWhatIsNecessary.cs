using System;
using System.Collections.Generic;
using NUnit.Framework;
using Octopus.Server.Extensibility.Tests;

namespace Sashimi.AzureResourceGroup.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary : OnlyExposeWhatIsNecessaryFixture
    {
        protected override Type EntryPointTypeUnderTest => typeof(AzureResourceGroupModule);

        protected override IEnumerable<Type> KnownClassesWhoAreBendingTheRules
        {
            get
            {
                yield break;
            }
        }
    }
}