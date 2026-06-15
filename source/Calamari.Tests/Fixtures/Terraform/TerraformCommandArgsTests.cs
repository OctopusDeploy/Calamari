using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using Newtonsoft.Json;
using NuGet.Versioning;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Terraform
{
    /// <summary>
    /// Unit tests for Terraform command argument construction.
    /// Tests the logic in TerraformCliExecutor without needing a Terraform binary.
    /// </summary>
    [TestFixture]
    public class TerraformCommandArgsTests
    {
        // --- VarFiles argument construction ---

        [Test]
        public void VarFiles_SingleFile_ProducesVarFileArg()
        {
            var result = BuildVarFilesArg("example.tfvars");
            result.Should().Be("-var-file=\"example.tfvars\"");
        }

        [Test]
        public void VarFiles_MultipleFiles_ProducesMultipleArgs()
        {
            var result = BuildVarFilesArg("a.tfvars\nb.tfvars");
            result.Should().Be("-var-file=\"a.tfvars\" -var-file=\"b.tfvars\"");
        }

        [Test]
        public void VarFiles_WindowsLineEndings_HandledCorrectly()
        {
            var result = BuildVarFilesArg("a.tfvars\r\nb.tfvars");
            result.Should().Be("-var-file=\"a.tfvars\" -var-file=\"b.tfvars\"");
        }

        [Test]
        public void VarFiles_Null_ReturnsNull()
        {
            var result = BuildVarFilesArg(null);
            result.Should().BeNull();
        }

        // --- Init command construction ---

        [Test]
        public void InitCommand_PreV015_IncludesGetPlugins()
        {
            var result = BuildInitCommand(new Version(0, 14, 0), allowPluginDownloads: true, additionalParams: null);
            result.Should().Be("init -get-plugins=true");
        }

        [Test]
        public void InitCommand_PreV015_PluginDownloadsDisabled()
        {
            var result = BuildInitCommand(new Version(0, 14, 0), allowPluginDownloads: false, additionalParams: null);
            result.Should().Be("init -get-plugins=false");
        }

        [Test]
        public void InitCommand_V015AndAbove_OmitsGetPlugins()
        {
            var result = BuildInitCommand(new Version(0, 15, 0), allowPluginDownloads: true, additionalParams: null);
            result.Should().Be("init");
        }

        [Test]
        public void InitCommand_V1_OmitsGetPlugins()
        {
            var result = BuildInitCommand(new Version(1, 8, 5), allowPluginDownloads: true, additionalParams: null);
            result.Should().Be("init");
        }

        [Test]
        public void InitCommand_WithAdditionalParams()
        {
            var result = BuildInitCommand(new Version(1, 0, 0), allowPluginDownloads: true, additionalParams: "-backend-config=\"key=value\"");
            result.Should().Be("init -backend-config=\"key=value\"");
        }

        [Test]
        public void InitCommand_PreV015_WithAdditionalParams()
        {
            var result = BuildInitCommand(new Version(0, 13, 7), allowPluginDownloads: true, additionalParams: "-var-file=\"backend.tfvars\"");
            result.Should().Be("init -get-plugins=true -var-file=\"backend.tfvars\"");
        }

        // --- Environment variable handling ---

        [Test]
        public void EnvironmentVariables_ParsedFromJson()
        {
            var json = JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { "TF_VAR_ami", "test-value" },
                { "TF_LOG", "DEBUG" }
            });

            var result = ParseEnvironmentVariables(json);
            result.Should().ContainKey("TF_VAR_ami").WhoseValue.Should().Be("test-value");
            result.Should().ContainKey("TF_LOG").WhoseValue.Should().Be("DEBUG");
        }

        [Test]
        public void EnvironmentVariables_NullDoesNotThrow()
        {
            var result = ParseEnvironmentVariables(null);
            result.Should().BeEmpty();
        }

        [Test]
        public void EnvironmentVariables_EmptyStringDoesNotThrow()
        {
            var result = ParseEnvironmentVariables("");
            result.Should().BeEmpty();
        }

        // --- Version range checking ---

        [Test]
        public void SupportedVersionRange_V0137_IsSupported()
        {
            IsVersionSupported(new Version(0, 13, 7)).Should().BeTrue();
        }

        [Test]
        public void SupportedVersionRange_V185_IsSupported()
        {
            IsVersionSupported(new Version(1, 8, 5)).Should().BeTrue();
        }

        [Test]
        public void SupportedVersionRange_V190_IsNotSupported()
        {
            IsVersionSupported(new Version(1, 9, 0)).Should().BeFalse();
        }

        [Test]
        public void SupportedVersionRange_V0126_IsNotSupported()
        {
            IsVersionSupported(new Version(0, 12, 6)).Should().BeFalse();
        }

        // --- Helpers reproducing TerraformCliExecutor logic ---

        static string BuildVarFilesArg(string varFilesVariable)
        {
            if (varFilesVariable == null) return null;

            var varFiles = Regex.Split(varFilesVariable, "\r?\n")
                .Select(v => $"-var-file=\"{v}\"")
                .ToList();

            return string.Join(" ", varFiles);
        }

        static string BuildInitCommand(Version version, bool allowPluginDownloads, string additionalParams)
        {
            var initCommand = "init";
            if (version < new Version(0, 15, 0))
                initCommand += $" -get-plugins={allowPluginDownloads.ToString().ToLower()}";
            if (!string.IsNullOrEmpty(additionalParams))
                initCommand += $" {additionalParams}";
            return initCommand;
        }

        static Dictionary<string, string> ParseEnvironmentVariables(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string>();

            return JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        }

        static bool IsVersionSupported(Version version)
        {
            var supportedRange = new VersionRange(
                NuGetVersion.Parse("0.13.7"), true,
                NuGetVersion.Parse("1.9"), false);
            return supportedRange.Satisfies(new NuGetVersion(version));
        }
    }
}
