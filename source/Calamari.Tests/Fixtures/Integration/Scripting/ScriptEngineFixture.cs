using System;
using System.Collections.Generic;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class ScriptEngineFixture
    {
        static readonly ScriptSyntax[] ScriptPreferencesNonWindows =
        {
            ScriptSyntax.Bash,
            ScriptSyntax.Python,
            ScriptSyntax.CSharp,
            ScriptSyntax.FSharp,
            ScriptSyntax.PowerShell
        };

        static readonly ScriptSyntax[] ScriptPreferencesWindows =
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
        {
            DeterminesCorrectScriptTypePreferenceOrder(ScriptPreferencesWindows);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void DeterminesCorrectScriptTypePreferencesOrderNonWindows()
        {
            DeterminesCorrectScriptTypePreferenceOrder(ScriptPreferencesNonWindows);
        }

        void DeterminesCorrectScriptTypePreferenceOrder(IEnumerable<ScriptSyntax> expected)
        {
            var engine = new ScriptEngine(new List<IScriptWrapper>(), Substitute.For<ILog>());
            var supportedTypes = engine.GetSupportedTypes();

            supportedTypes.Should().Equal(expected);
        }
    }
}