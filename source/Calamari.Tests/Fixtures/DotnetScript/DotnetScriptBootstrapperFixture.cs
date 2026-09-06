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
        [TestCase(null, null, null)]
        [TestCase("-- \"Parameter 1\" \"Parameter 2\"", null, "\"Parameter 1\" \"Parameter 2\"")]
        [TestCase("\"Parameter 1\" \"Parameter 2\"", null, "\"Parameter 1\" \"Parameter 2\"")]
        [TestCase("--isolated-load-context -- \"Parameter 1\" \"Parameter 2\"", "--isolated-load-context ", "\"Parameter 1\" \"Parameter 2\"")]
        [TestCase("--isolated-load-context -d -- \"Parameter 1\" \"Parameter 2\"", "--isolated-load-context -d ", "\"Parameter 1\" \"Parameter 2\"")]
        [TestCase("--isolated-load-context --verbosity debug -- \"Parameter 1\" \"Parameter 2\"", "--isolated-load-context --verbosity debug ", "\"Parameter 1\" \"Parameter 2\"")]
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
        public void FormatCommandArguments_DoesNotDisableIsolatedLoadContext_ByDefault()
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
            var bootstrapFile = "Bootstrap.csx";
            var result = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, "--verbosity debug -- \"Parameter 1\"", null, true);
            result.IndexOf("--disable-isolated-load-context", StringComparison.Ordinal)
                  .Should()
                  .BeLessThan(result.IndexOf($"\"{bootstrapFile}\"", StringComparison.Ordinal));
        }
    }
}