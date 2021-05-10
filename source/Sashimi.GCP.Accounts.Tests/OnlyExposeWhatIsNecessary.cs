using System;
using System.Collections.Generic;
using NUnit.Framework;
using Octopus.Server.Extensibility.Tests;
using Sashimi.GCP.Accounts.Validation;

namespace Sashimi.GCP.Accounts.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary : OnlyExposeWhatIsNecessaryFixture
    {
        protected override Type EntryPointTypeUnderTest => typeof(GcpAccountModule);

        protected override IEnumerable<Type> KnownClassesWhoAreBendingTheRules
        {
            get
            {
                yield return typeof(AccountTypes);
                yield return typeof(GcpDeploymentValidatorBase);
            }
        }
    }
}