using System;
using System.IO;
using System.Reflection;
using Autofac;
using Calamari.Integration.Processes;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Tests.Fixtures.Commands
{
    [TestFixture]
    public class HelpTestUnknown
    {
        private IContainer container;

        private string[] Args => new[] {"help", "unknown"};
        
        [SetUp]
        public void SetUp()
        {
            container = Calamari.Program.BuildContainer(Args);
        }

        [TearDown]
        public void TearDown()
        {
            container?.Dispose();
        }

        [Test]
        public void RunScript()
        {
            var retCode = container.Resolve<Calamari.Program>().Execute(Args);
            Assert.AreEqual(0, retCode);
        }
    }
}
