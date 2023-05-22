using Calamari.Integration.Processes;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using System;
using System.IO;
using System.Reflection;
using Calamari.Integration.FileSystem;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;

namespace Calamari.Tests.Fixtures.Commands
{
    [TestFixture]
    public class RunScriptTest
    {
        [Test]
        public void RunScript()
        {
            var program = new TestCalamariRunner(new InMemoryLog());
            var retCode = program.RunStubCommand();
            
            retCode.Should().Be(0);
            program.StubWasCalled.Should().BeTrue();
        }
    }
}
