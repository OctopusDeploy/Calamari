using System;
using System.Collections.Generic;
using NUnit.Framework;
using Octopus.Server.Extensibility.Tests;

namespace Sashimi.AzureScripting.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary : OnlyExposeWhatIsNecessaryFixture
    {
        protected override Type EntryPointTypeUnderTest => typeof(AzureScriptingModule);

        protected override IEnumerable<Type> KnownClassesWhoAreBendingTheRules
        {
            get
            {
                yield return typeof(AzureActionHandlerExtensions);
                yield return typeof(AzurePowerShellActionHandler);
            }
        }
    }
}