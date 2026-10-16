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
        public void FormatCommandArguments_DisablesIsolatedLoadContext_ByDefault()
        {
            var result = DotnetScriptBootstrapper.FormatCommandArguments("Bootstrap.csx", null);
            result.Should().Contain("--disable-isolated-load-context ");
        }

        [TestCase("--isolated-load-context -- \"Parameter 1\"")]
        [TestCase("--isolated-load-context -d -- \"Parameter 1\"")]
        public void FormatCommandArguments_LeavesIsolationOn_WhenTheCallerAskedForIt(string scriptParameters)
        {
            // Both flags together resolve to disabled, so we must not add ours on top of theirs.
            var result = DotnetScriptBootstrapper.FormatCommandArguments("Bootstrap.csx", scriptParameters);
            result.Should().NotContain("--disable-isolated-load-context");
            result.Should().Contain("--isolated-load-context ");
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
    }
}