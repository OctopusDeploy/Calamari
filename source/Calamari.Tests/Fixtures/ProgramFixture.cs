using System;
using System.Net;
using System.Text.RegularExpressions;
using Calamari.Common;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Deployment;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures
{
    [Ignore("Testing assumption that proxy is causing havoc")]
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
            // This usage of Environment.GetEnvironmentVariable is fine as it's not accessing a test dependency variable
            Environment.GetEnvironmentVariable("OctopusCalamariWorkingDirectory").Should().NotBeNullOrWhiteSpace();
        }
        
        [Test]
        public void ProxyIsInitialized()
        {
            var existingProxy = WebRequest.DefaultWebProxy;
            try
            {
                EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", "localhost");

                WebRequest.DefaultWebProxy = null;
                RunProgram();
                WebRequest.DefaultWebProxy.Should().NotBeNull();
            }
            finally
            {
                WebRequest.DefaultWebProxy = existingProxy;
                EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", null);
            }
        }

        [Test]
        public void DefaultRegexMatchTimeoutIsSet()
        {
            RunProgram();
            var regexTimeout = AppDomain.CurrentDomain.GetData("REGEX_DEFAULT_MATCH_TIMEOUT");
            regexTimeout.Should().Be(AppDomainConfiguration.DefaultRegexMatchTimeout);
        }
    }
}