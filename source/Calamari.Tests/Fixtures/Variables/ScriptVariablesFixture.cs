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
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void FormatScriptNameForPhysicalFilesystem_ShouldRemoveSemiColon_OnWindows()
        {
            var result = ScriptVariables.FormatScriptNameForPhysicalFilesystem("Script:Name");
            result.Should().Be("ScriptName");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void FormatScriptNameForPhysicalFilesystem_ShouldNotRemoveSemiColon_OnUnix()
        {
            var result = ScriptVariables.FormatScriptNameForPhysicalFilesystem("Script:Name");
            result.Should().Be("Script:Name");
        }
        
        [Test]
        public void FormatScriptNameForPhysicalFilesystem_ShouldNotRemoveHyphens()
        {
            var result = ScriptVariables.FormatScriptNameForPhysicalFilesystem("Script-Name");
            result.Should().Be("Script-Name");
        }
        
        [Test]
        public void FormatScriptNameForPhysicalFilesystem_ShouldNotRemoveUnderscores()
        {
            var result = ScriptVariables.FormatScriptNameForPhysicalFilesystem("Script_Name");
            result.Should().Be("Script_Name");
        }
        
        [Test]
        public void FormatScriptNameForPhysicalFilesystem_ShouldRemoveSpaces()
        {
            var result = ScriptVariables.FormatScriptNameForPhysicalFilesystem("Script Name");
            result.Should().Be("ScriptName");
        }
        
        [Test]
        public void FormatScriptNameForPhysicalFilesystem_ShouldHandleEmptyString()
        {
            var result = ScriptVariables.FormatScriptNameForPhysicalFilesystem("");
            result.Should().Be("");
        }
    }
}