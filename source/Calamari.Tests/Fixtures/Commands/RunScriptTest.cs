using Autofac;
using Calamari.Integration.Processes;
using NUnit.Framework;
using Octopus.CoreUtilities.Extensions;
using System;
using System.IO;
using System.Reflection;

namespace Calamari.Tests.Fixtures
{
    [TestFixture]
    public class RunScriptTest
    {
        private IContainer container;

        private string[] Args => new[] {"run-script"};
        

        [SetUp]
        public void SetUp()
        {
            EnvironmentVariables.EnsureVariablesExist();
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
            // Expected because we don't pass the required variables
            Assert.AreEqual(1, retCode);
        }
    }
}
