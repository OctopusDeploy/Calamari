using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Shared;
using Calamari.Shared.Scripting;
using NSubstitute;
using NUnit.Framework;
using Script = Calamari.Integration.Scripting.Script;

namespace Calamari.Tests.Hooks
{
    [TestFixture]
    public class ScriptWrapperTest
    {
        private IScriptEngine innerEngine;
        private IScriptEngineRegistry engineRegistry;
        private ICommandLineRunner commandLineRunner;
        [SetUp]
        public void SetUp()
        {
            innerEngine = Substitute.For<IScriptEngine>();
            engineRegistry = Substitute.For<IScriptEngineRegistry>();
            engineRegistry.ScriptEngines.Returns(new Dictionary<ScriptSyntax, IScriptEngine>() { [ScriptSyntax.CSharp] = innerEngine});
            
            commandLineRunner = Substitute.For<ICommandLineRunner>();
        }
        
        [Test]
        public void InvokesCustomScriptHandler()
        {
            var sw = new ScriptHookMock();

            var combined = new CombinedScriptEngine(engineRegistry, new []{ sw });
            combined.Execute(new Script("Foo.csx", "Bar"), new CalamariVariableDictionary(), commandLineRunner, new StringDictionary());

            Assert.IsTrue(sw.WasCalled);
        }
        
        [Test]
        public void SkipsCustomScriptHandlerIfNotEnabled()
        {
            var sw = new ScriptHookMock(false);

            var combined = new CombinedScriptEngine(engineRegistry, new []{ sw });
            combined.Execute(new Script("Foo.csx", "Bar"), new CalamariVariableDictionary(), commandLineRunner, new StringDictionary());

            Assert.IsFalse(sw.WasCalled);
        }
        
        [Test]
        public void InvokesInnerScriptHandler()
        {
            var combined = new CombinedScriptEngine(engineRegistry, new []{ new ScriptHookMock() });

            var calamariVariables = new CalamariVariableDictionary();
            var environmentVariables = new StringDictionary();
          
            combined.Execute(new Script("Foo.csx", "Bar"), calamariVariables, commandLineRunner, environmentVariables);

            innerEngine.Received().Execute(Arg.Any<Script>(), calamariVariables, commandLineRunner, environmentVariables);
        }
        
        [Test]
        public void ReturnsInnerScriptResult()
        {
            var combined = new CombinedScriptEngine(engineRegistry, new []{ new ScriptHookMock() });

            var calamariVariables = new CalamariVariableDictionary();
            var environmentVariables = new StringDictionary();

            var commandResult = new CommandResult("CMD", 55);
            innerEngine.Execute(Arg.Any<Script>(), calamariVariables, commandLineRunner, environmentVariables)
                .Returns(commandResult);
            
            var result = combined.Execute(new Script("Foo.csx", "Bar"), calamariVariables, commandLineRunner, environmentVariables);

            Assert.AreEqual(commandResult, result);
        }
        
    }
}