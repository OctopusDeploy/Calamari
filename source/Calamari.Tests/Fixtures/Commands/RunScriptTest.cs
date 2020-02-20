using Autofac;
using Calamari.Integration.Processes;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using System;
using System.IO;
using System.Reflection;
using Calamari.Integration.FileSystem;

namespace Calamari.Tests.Fixtures
{
    [TestFixture]
    public class RunScriptTest
    {
        [Test]
        public void RunScript()
        {
            var retCode = Program.Main(new[] {"run-script"});
            // Expected because we don't pass the required variables
            Assert.AreEqual(1, retCode);
        }
    }
}
