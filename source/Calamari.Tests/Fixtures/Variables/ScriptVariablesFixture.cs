using System;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Variables
{
    [TestFixture]
    public class ScriptVariablesFixture
    {
        [Test]
        [TestCase("MyScriptModule", "MyScriptModule")]
        [TestCase("My-Script-Module", "MyScriptModule")]
        [TestCase("My Script Module", "MyScriptModule")]
        [TestCase("My_Script_Module", "My_Script_Module")]
        [TestCase("My-Script_ Module", "MyScript_Module")]
        public void FormatScriptName_ShouldCorrectlyFormatFilenames(string input, string expected)
        {
            var result = ScriptVariables.FormatScriptName(input);
            result.Should().Be(expected);
        }
    }
}