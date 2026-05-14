using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Calamari.Common;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Deployment;
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

        static async Task<int> RunProgram()
            => await Program.Main(["run-script"]);

        [Test]
        public async Task OctopusCalamariWorkingDirectoryEnvironmentVariableIsSet()
        {
            EnvironmentHelper.SetEnvironmentVariable("OctopusCalamariWorkingDirectory", null);
            await RunProgram();
            // This usage of Environment.GetEnvironmentVariable is fine as it's not accessing a test dependency variable
            Environment.GetEnvironmentVariable("OctopusCalamariWorkingDirectory").Should().NotBeNullOrWhiteSpace();
        }
        
        [Test]
        public async Task ProxyIsInitialized()
        {
            var existingProxy = WebRequest.DefaultWebProxy;
            try
            {
                EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", "localhost");

                WebRequest.DefaultWebProxy = null;
                await RunProgram();
                WebRequest.DefaultWebProxy.Should().NotBeNull();
            }
            finally
            {
                WebRequest.DefaultWebProxy = existingProxy;
                EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", null);
            }
        }

        [Test]
        public async Task DefaultRegexMatchTimeoutIsSet()
        {
            await RunProgram();
            var regexTimeout = AppDomain.CurrentDomain.GetData("REGEX_DEFAULT_MATCH_TIMEOUT");
            regexTimeout.Should().Be(AppDomainConfiguration.DefaultRegexMatchTimeout);
        }
    }
}