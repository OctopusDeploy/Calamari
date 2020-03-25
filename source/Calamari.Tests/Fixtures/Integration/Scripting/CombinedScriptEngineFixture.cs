using System.Collections.Generic;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using FluentAssertions;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class CombinedScriptEngineFixture
    {
        private static readonly ScriptSyntax[] ScriptPreferencesNonWindows = new[]
        {
            ScriptSyntax.Bash,
            ScriptSyntax.Python,
            ScriptSyntax.CSharp,
            ScriptSyntax.FSharp,
            ScriptSyntax.PowerShell
        };

        private static readonly ScriptSyntax[] ScriptPreferencesWindows = new[]
        {
            ScriptSyntax.PowerShell,
            ScriptSyntax.Python,
            ScriptSyntax.CSharp,
            ScriptSyntax.FSharp,
            ScriptSyntax.Bash
        };

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void DeterminesCorrectScriptTypePreferenceOrderWindows()
            => DeterminesCorrectScriptTypePreferenceOrder(ScriptPreferencesWindows);

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void DeterminesCorrectScriptTypePreferencesOrderNonWindows()
            => DeterminesCorrectScriptTypePreferenceOrder(ScriptPreferencesNonWindows);

        private void DeterminesCorrectScriptTypePreferenceOrder(IEnumerable<ScriptSyntax> expected)
        {
            var engine = new CombinedScriptEngine(null);
            var supportedTypes = engine.GetSupportedTypes();

            supportedTypes.Should().Equal(expected);
        }
    }
}