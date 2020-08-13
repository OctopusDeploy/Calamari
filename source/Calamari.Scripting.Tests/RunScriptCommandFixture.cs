using System;
using System.Threading.Tasks;
using Calamari.Tests.Shared;
using FluentAssertions;
using NUnit.Framework;
using Sashimi.Server.Contracts;
using KnownVariables = Sashimi.Server.Contracts.KnownVariables;
using ScriptSyntax = Calamari.Common.Features.Scripts.ScriptSyntax;

namespace Calamari.Scripting.Tests
{
    [TestFixture]
    public class RunScriptCommandFixture
    {
        [Test]
        public Task ExecuteInlineScript()
        {
            var psScript = "echo 'Hello #{Name}'";
            return CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                                     .WithArrange(context =>
                                                  {
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline);
                                                      context.Variables.Add(KnownVariables.Action.Script.Syntax, ScriptSyntax.PowerShell.ToString());
                                                      context.Variables.Add(KnownVariables.Action.Script.ScriptBody, psScript);
                                                      context.Variables.Add("Name", "World");
                                                  })
                                     .WithAssert(result => result.FullLog.Should().Contain("Hello World"))
                                     .Execute();
        }
    }
}