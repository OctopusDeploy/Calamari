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
    public class HelpTest
    {
        private IContainer container;

        private string[] Args => new[] {"help", "run-script"};
        
        [SetUp]
        public void SetUp()
        {
            ExternalVariables.LogMissingVariables();
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
