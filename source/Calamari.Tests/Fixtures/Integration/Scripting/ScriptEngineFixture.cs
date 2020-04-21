using System.Collections.Generic;
using Calamari.Hooks;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using Calamari.Variables;
using NUnit.Framework;
using FluentAssertions;
using NSubstitute;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    public class ScriptEngineVariableHandlingFixture
    {
        class ScriptWrapperOne : ScriptWrapperBase
        {
            public override int Priority => 1;
            public override bool IsEnabled(ScriptSyntax syntax) => true;
            public override IScriptWrapper NextWrapper { get; set; }
            protected override CommandResult ExecuteScriptBase(Script script, ScriptSyntax scriptSyntax, ICommandLineRunner commandLineRunner,
                Dictionary<string, string> environmentVars)
            {
                Variables.Set("OctopusAzureTargetScript", "ValueOne");
                return NextWrapper.ExecuteScript(new Script("Script-One.ps1"), scriptSyntax, commandLineRunner, Variables, environmentVars);
            }
        }
        
        class ScriptWrapperTwo : ScriptWrapperBase
        {
            public override int Priority => 1;
            public override bool IsEnabled(ScriptSyntax syntax) => true;
            public override IScriptWrapper NextWrapper { get; set; }
            protected override CommandResult ExecuteScriptBase(Script script, ScriptSyntax scriptSyntax, ICommandLineRunner commandLineRunner,
                Dictionary<string, string> environmentVars)
            {
                Variables.Set("OctopusAzureTargetScript", "ValueTwo");
                return NextWrapper.ExecuteScript(new Script("Script-Two.ps1"), scriptSyntax, commandLineRunner, Variables, environmentVars);
            }
        }

        [Test]
        public void ScriptExecution_ShouldNotAllowScriptWrappersToOverwriteVariables()
        {
            var variables = new CalamariVariables();
            variables.Set("OctopusAzureTargetScript", "ExpectedValue");
            
            var runner = Substitute.For<ICommandLineRunner>();
            runner.Execute(Arg.Any<CommandLineInvocation>()).Returns(new CommandResult("command", 0));
            
            var sut = new ScriptEngine(new IScriptWrapper[] { new ScriptWrapperOne(), new ScriptWrapperTwo() });
            
            sut.Execute(new Script("My-Script.ps1", "Parameters"), variables, runner);

            variables.Get("OctopusAzureTargetScript").Should().Be("ExpectedValue");
        }
    }
    
    [TestFixture]
    public class ScriptEngineFixture
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
            var engine = new ScriptEngine(null);
            var supportedTypes = engine.GetSupportedTypes();

            supportedTypes.Should().Equal(expected);
        }
        
    }
}