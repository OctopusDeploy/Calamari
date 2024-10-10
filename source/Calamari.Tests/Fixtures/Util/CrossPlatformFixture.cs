using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using Calamari.Util;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Util
{
    [TestFixture]
    public class CrossPlatformFixture
    {
        [OneTimeSetUp]
        public void SetEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable("MARIO_BROTHER", "LUIGI");
        }

        [OneTimeTearDown]
        public void ClearEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable("MARIO_BROTHER", null);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void TildePrefixReplacedWithHome()
        {
            // This usage of Environment.GetEnvironmentVariable is fine as it's not accessing a test dependency variable
            var home = Environment.GetEnvironmentVariable("HOME");
            Assert.IsNotNull(home, "Expected $HOME environment variable to be set.");

            var value = CrossPlatform.ExpandPathEnvironmentVariables("~/blah");
            Assert.AreEqual($"{home}/blah", value);
        }

        [Test]
        [TestCase("$MARIO_BROTHER/blah", "LUIGI/blah")]
        [TestCase("%MARIO_BROTHER%/blah", "LUIGI/blah", Description = "Windows style variables included in Nix")]
        [TestCase("$MARIO_BROTHERZZ/blah", "/blah", Description = "Variables terminate at last non alpha numeric character")]
        [TestCase("IMA$MARIO_BROTHER", "IMALUIGI", Description = "Variables begin from dollar character")]
        [TestCase("\\$MARIO_BROTHER/blah", "\\$MARIO_BROTHER/blah", Description = "Escaped dollar preserved")]
        [TestCase("$MARIO_BROTHER/blah%2fblah.zip", "LUIGI/blah%2fblah.zip", Description = "URL-encoded forward slash (%2f) is preserved")]
        [TestCase("%MARIO_BROTHER%/blah%2fblah.zip", "LUIGI/blah%2fblah.zip", Description = "URL-encoded forward slash (%2f) prevents Windows-style variable expansion")]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void NixEnvironmentVariableReplaced(string inputValue, string expectedResult)
        {
            var value = CrossPlatform.ExpandPathEnvironmentVariables(inputValue);
            Assert.AreEqual(expectedResult, value);
        }

        [Test]
        [TestCase("$MARIO_BROTHER/blah", "$MARIO_BROTHER/blah", Description = "Nix variable format ignored")]
        [TestCase("IMA%MARIO_BROTHER%PLUMBER", "IMALUIGIPLUMBER", Description = "Variables demarcated by percent character")]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void WindowsEnvironmentVariableReplaced(string inputValue, string expectedResult)
        {
            var value = CrossPlatform.ExpandPathEnvironmentVariables(inputValue);
            Assert.AreEqual(expectedResult, value);
        }
    }
}
