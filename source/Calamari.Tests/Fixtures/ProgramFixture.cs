using System;
using System.Net;
using Calamari.Deployment;
using Calamari.Util.Environments;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures
{
    public class ProgramFixture
    {
        [Test]
        public void RunScript()
        {
            var retCode = RunProgram();
            // Expected because we don't pass the required variables
            Assert.AreEqual(1, retCode);
        }

        static int RunProgram()
            => Program.Main(new[] {"run-script"});

        [Test]
        public void OctopusCalamariWorkingDirectoryEnvironmentVariableIsSet()
        {
            EnvironmentHelper.SetEnvironmentVariable("OctopusCalamariWorkingDirectory", null);
            RunProgram();
            Environment.GetEnvironmentVariable("OctopusCalamariWorkingDirectory").Should().NotBeNullOrWhiteSpace();
        }
        
        [Test]
        public void ProxyIsInitialized()
        {
            try
            {
                EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", "localhost");

                WebRequest.DefaultWebProxy = null;
                RunProgram();
                WebRequest.DefaultWebProxy.Should().NotBeNull();
            }
            finally
            {
                EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", null);
            }
        }
    }
}