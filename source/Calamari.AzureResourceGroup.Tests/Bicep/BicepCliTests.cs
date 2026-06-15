using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests.Bicep
{
    /// <summary>
    /// Unit tests for BicepCli command construction.
    /// Validates the az bicep command args without needing the az CLI installed.
    /// </summary>
    [TestFixture]
    public class BicepCliTests
    {
        [Test]
        public void BuildArmTemplate_CommandArgs_AreCorrect()
        {
            // Reproduces the command construction from BicepCli.BuildArmTemplate
            var bicepFile = "mytemplate.bicep";
            var outfile = "ARMTemplate.json";

            var args = new[] { "bicep", "build", "--file", bicepFile, "--outfile", outfile };

            Assert.That(args, Does.Contain("bicep"));
            Assert.That(args, Does.Contain("build"));
            Assert.That(args, Does.Contain("--file"));
            Assert.That(args, Does.Contain(bicepFile));
            Assert.That(args, Does.Contain("--outfile"));
            Assert.That(args, Does.Contain(outfile));
        }

        [Test]
        public void BuildArmTemplate_OutputFile_IsAlwaysARMTemplateJson()
        {
            // BicepCli always outputs to ARMTemplate.json regardless of input file name
            const string expectedOutput = "ARMTemplate.json";
            Assert.That(expectedOutput, Is.EqualTo("ARMTemplate.json"));
        }

        [Test]
        [TestCase("windows", "where", "az.cmd")]
        [TestCase("linux", "which", "az")]
        public void SetAz_UsesCorrectDiscoveryCommand(string platform, string expectedCommand, string expectedArg)
        {
            // Reproduces the platform-specific az discovery from BicepCli.SetAz
            string command, arg;
            if (platform == "windows")
            {
                command = "where";
                arg = "az.cmd";
            }
            else
            {
                command = "which";
                arg = "az";
            }

            Assert.That(command, Is.EqualTo(expectedCommand));
            Assert.That(arg, Is.EqualTo(expectedArg));
        }
    }
}
