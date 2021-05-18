using System;
using NUnit.Framework;
using Octopus.Server.Extensibility.Tests;
using Sashimi.GCPScripting;

namespace Sashimi.GoogleCloud.Scripting.Tests
{
    [TestFixture]
    public class OnlyExposeWhatIsNecessary : OnlyExposeWhatIsNecessaryFixture
    {
        protected override Type EntryPointTypeUnderTest => typeof(GoogleCloudScriptingModule);
    }
}