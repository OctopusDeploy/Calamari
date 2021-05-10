using System;
using System.Collections.Generic;
using NUnit.Framework;
using Octopus.Server.Extensibility.Tests;
using Sashimi.GoogleCloud.Accounts.Validation;

namespace Sashimi.GoogleCloud.Accounts.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary : OnlyExposeWhatIsNecessaryFixture
    {
        protected override Type EntryPointTypeUnderTest => typeof(GoogleCloudAccountModule);

        protected override IEnumerable<Type> KnownClassesWhoAreBendingTheRules
        {
            get
            {
                yield return typeof(AccountTypes);
                yield return typeof(GoogleCloudDeploymentValidatorBase);
            }
        }
    }
}