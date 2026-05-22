using System;
using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
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
            ScriptSyntax.PowerShell
        };

        static readonly ScriptSyntax[] ScriptPreferencesWindows =
        {
            ScriptSyntax.PowerShell,
            ScriptSyntax.Python,
            ScriptSyntax.CSharp,
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
        
        [Test]
          public void ExecuteWithoutEnvironmentVarsPassesEmptyDictionaryToWrappers()
          {
              Dictionary<string, string> receivedEnvironmentVars = null;
              var capturingWrapper = new CapturingScriptWrapper(envVars => receivedEnvironmentVars = envVars);
  
              var engine = new ScriptEngine(new List<IScriptWrapper> { capturingWrapper }, Substitute.For<ILog>());
              var variables = new CalamariVariables();
              var runner = Substitute.For<ICommandLineRunner>();
  
              engine.Execute(new Script("test.ps1"), variables, runner);
  
              receivedEnvironmentVars.Should().NotBeNull();
              receivedEnvironmentVars.Should().BeEmpty();
          }
  
          [Test]
          public void ExecuteWithEnvironmentVarsPassesDictionaryToWrappers()
          {
              Dictionary<string, string> receivedEnvironmentVars = null;
              var capturingWrapper = new CapturingScriptWrapper(envVars => receivedEnvironmentVars = envVars);
  
              var engine = new ScriptEngine(new List<IScriptWrapper> { capturingWrapper }, Substitute.For<ILog>());
              var variables = new CalamariVariables();
              var runner = Substitute.For<ICommandLineRunner>();
              var envVars = new Dictionary<string, string> { { "AWS_REGION", "us-east-1" } };
  
              engine.Execute(new Script("test.ps1"), variables, runner, envVars);
  
              receivedEnvironmentVars.Should().NotBeNull();
              receivedEnvironmentVars.Should().ContainKey("AWS_REGION");
          }
  
          /// <summary>
          /// A wrapper that captures the environmentVars it receives, to verify they are never null.
          /// </summary>
          class CapturingScriptWrapper : IScriptWrapper
          {
              readonly Action<Dictionary<string, string>> onExecute;
  
              public CapturingScriptWrapper(Action<Dictionary<string, string>> onExecute)
              {
                  this.onExecute = onExecute;
             }
 
             public int Priority => ScriptWrapperPriorities.ToolConfigPriority;
             public IScriptWrapper NextWrapper { get; set; }
 
             public bool IsEnabled(ScriptSyntax syntax) => true;
 
             public CommandResult ExecuteScript(Script script, ScriptSyntax scriptSyntax, ICommandLineRunner commandLineRunner, Dictionary<string, string> environmentVars)
             {
                 onExecute(environmentVars);
                 return new CommandResult("captured", 0);
             }
         }

        void DeterminesCorrectScriptTypePreferenceOrder(IEnumerable<ScriptSyntax> expected)
        {
            var engine = new ScriptEngine(new List<IScriptWrapper>(), Substitute.For<ILog>());
            var supportedTypes = engine.GetSupportedTypes();

            supportedTypes.Should().Equal(expected);
        }
    }
}