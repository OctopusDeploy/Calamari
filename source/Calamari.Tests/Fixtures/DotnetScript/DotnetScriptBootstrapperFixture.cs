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
        public void FormatCommandArgumentsTest(string scriptParameters, string commandArguments, string scriptArguments)
        {
            var bootstrapFile = "Bootstrap." + Guid.NewGuid().ToString().Substring(10) + "." + "Script.csx";
            var formattedCommandArgument = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, scriptParameters);
            formattedCommandArgument.Should().Contain($"{commandArguments}\"{bootstrapFile}\" -- {scriptArguments}");
        }
    }
}