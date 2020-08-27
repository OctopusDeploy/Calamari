using System;
using NUnit.Framework;
using Octopus.Server.Extensibility.Tests;

namespace Sashimi.NamingIsHard.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary : OnlyExposeWhatIsNecessaryFixture
    {
        protected override Type EntryPointTypeUnderTest => typeof(NamingIsHardModule);
    }
}