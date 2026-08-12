using System;
using Calamari.Common.Features.Scripting.DotnetScript;
using FluentAssertions;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.DotnetScript
{
    [TestFixture]
    public class DotnetScriptBootstrapperFixture
    {
        // The --isolated-load-context cases expect the flag to be gone: dotnet-script 2.0 no longer
        // recognises it and would forward it into the script's own arguments. Every other option the
        // caller passes is left exactly where it was.
        [TestCase(null, null, null)]
        [TestCase("-- \"Parameter 1\" \"Parameter 2\"", null, "\"Parameter 1\" \"Parameter 2\"")]
        [TestCase("\"Parameter 1\" \"Parameter 2\"", null, "\"Parameter 1\" \"Parameter 2\"")]
        [TestCase("--isolated-load-context -- \"Parameter 1\" \"Parameter 2\"", null, "\"Parameter 1\" \"Parameter 2\"")]
        [TestCase("--isolated-load-context -d -- \"Parameter 1\" \"Parameter 2\"", "-d ", "\"Parameter 1\" \"Parameter 2\"")]
        [TestCase("--isolated-load-context --verbosity debug -- \"Parameter 1\" \"Parameter 2\"", "--verbosity debug ", "\"Parameter 1\" \"Parameter 2\"")]
        [TestCase("--verbosity debug -- \"Parameter 1\" \"Parameter 2\"", "--verbosity debug ", "\"Parameter 1\" \"Parameter 2\"")]
        public void FormatCommandArgumentsTest([CanBeNull] string scriptParameters, [CanBeNull] string commandArguments, [CanBeNull] string scriptArguments)
        {
            var bootstrapFile = "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + "Script.csx";
            var formattedCommandArgument = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, scriptParameters);
            formattedCommandArgument.Should().Contain($"{commandArguments}\"{bootstrapFile}\" -- {scriptArguments}");
        }

        [Test]
        public void FormatCommandArguments_UsesCustomNuGetSource_WhenProvided()
        {
            var bootstrapFile = "Bootstrap.csx";
            var customSource = "https://my.internal.nuget/v3/index.json";
            var result = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, null, customSource);
            result.Should().Contain($"-s {customSource} ");
            result.Should().NotContain("api.nuget.org");
        }

        [Test]
        public void FormatCommandArguments_LeavesIsolationOn_ByDefault()
        {
            var result = DotnetScriptBootstrapper.FormatCommandArguments("Bootstrap.csx", null);
            result.Should().NotContain("--disable-isolated-load-context");
        }

        [Test]
        public void FormatCommandArguments_DisablesIsolatedLoadContext_WhenRequested()
        {
            var result = DotnetScriptBootstrapper.FormatCommandArguments("Bootstrap.csx", null, null, true);
            result.Should().Contain("--disable-isolated-load-context ");
        }

        [Test]
        public void FormatCommandArguments_PlacesDisableIsolatedLoadContextBeforeTheScriptFile()
        {
            // Anything after the bootstrap file is passed to the script, not to dotnet-script.
            const string bootstrapFile = "Bootstrap.csx";
            var result = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, "--verbosity debug -- \"Parameter 1\"", null, true);
            result.IndexOf("--disable-isolated-load-context", StringComparison.Ordinal)
                  .Should()
                  .BeLessThan(result.IndexOf($"\"{bootstrapFile}\"", StringComparison.Ordinal));
        }

        [Test]
        public void FormatCommandArguments_KeepsTheOptOutFlag_WhenTheCallerAlsoPassedTheLegacyOptIn()
        {
            // The legacy opt-in is dropped rather than treated as a conflicting instruction, so the
            // step variable stays authoritative and the caller's flag cannot corrupt their arguments.
            var result = DotnetScriptBootstrapper.FormatCommandArguments("Bootstrap.csx", "--isolated-load-context -- P0", null, true);
            result.Should().Contain("--disable-isolated-load-context ");
            result.Should().NotContain(" --isolated-load-context");
        }

        [TestCase("--isolated-load-context", "")]
        [TestCase("--ISOLATED-LOAD-CONTEXT", "")]
        [TestCase("--isolated-load-context -d", "-d")]
        [TestCase("-d --isolated-load-context", "-d")]
        [TestCase("--disable-isolated-load-context", "--disable-isolated-load-context")]
        [TestCase("--verbosity debug", "--verbosity debug")]
        [TestCase("", "")]
        [TestCase(null, null)]
        public void RemoveLegacyIsolatedLoadContextFlag_StripsWholeTokensOnly([CanBeNull] string input, [CanBeNull] string expected)
        {
            DotnetScriptBootstrapper.RemoveLegacyIsolatedLoadContextFlag(input).Should().Be(expected);
        }

        [TestCase("--isolated-load-context -- P0", true)]
        [TestCase("--disable-isolated-load-context -- P0", false)]
        [TestCase("-- P0", false)]
        [TestCase(null, false)]
        public void HasLegacyIsolatedLoadContextFlag_DetectsOnlyTheLegacyOptIn([CanBeNull] string scriptParameters, bool expected)
        {
            DotnetScriptBootstrapper.HasLegacyIsolatedLoadContextFlag(scriptParameters).Should().Be(expected);
        }
    }
}