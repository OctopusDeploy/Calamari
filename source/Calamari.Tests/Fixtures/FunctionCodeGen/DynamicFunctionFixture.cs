using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Assent;
using Calamari.Common;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.FunctionScriptContributions;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.FunctionCodeGen
{
    [TestFixture]
    [RequiresNonFreeBSDPlatform]
    [Category(TestCategory.PlatformAgnostic)]
    public class DynamicFunctionFixture
    {
        [Test]
        public Task EnsurePowerShellLanguageCodeGen()
        {
            using (var scriptFile = new TemporaryFile(Path.Combine(Path.GetTempPath(), Path.GetTempFileName())))
            {
                var serializedRegistrations = JsonConvert.SerializeObject(new[]
                {
                    new ScriptFunctionRegistration("MyFunc",
                                                   String.Empty,
                                                   "create-something",
                                                   new Dictionary<string, FunctionParameter>
                                                   {
                                                       { "mystring", new FunctionParameter(ParameterType.String) },
                                                       { "myboolean", new FunctionParameter(ParameterType.Bool) },
                                                       { "mynumber", new FunctionParameter(ParameterType.Int) }
                                                   }),
                    new ScriptFunctionRegistration("MyFunc2",
                                                   String.Empty,
                                                   "create-something2",
                                                   new Dictionary<string, FunctionParameter>
                                                   {
                                                       { "mystring", new FunctionParameter(ParameterType.String, "myboolean") },
                                                       { "myboolean", new FunctionParameter(ParameterType.Bool) },
                                                       { "mynumber", new FunctionParameter(ParameterType.Int, "myboolean") }
                                                   })
                });

                return CommandTestBuilder.CreateAsync<MyCommand, MyProgram>()
                                         .WithArrange(context =>
                                                      {
                                                          context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.CustomScripts);
                                                          var script = @"New-MyFunc -mystring 'Hello' -myboolean -mynumber 1
New-MyFunc2 -mystring 'Hello' -myboolean -mynumber 1
New-MyFunc2 -mystring 'Hello' -mynumber 1";
                                                          context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.Deploy, ScriptSyntax.PowerShell), script);
                                                          context.Variables.Add(ScriptFunctionsVariables.Registration, serializedRegistrations);
                                                          context.Variables.Add(ScriptFunctionsVariables.CopyScriptWrapper, scriptFile.FilePath);
                                                      })
                                         .WithAssert(result =>
                                                     {
                                                         var script = File.ReadAllText(scriptFile.FilePath);

                                                         this.Assent(script, AssentConfiguration.Default);
                                                     })
                                         .Execute();
            }
        }

        class MyProgram : CalamariFlavourProgramAsync
        {
            public MyProgram(ILog log) : base(log)
            {

            }
        }

        [Command("mycommand")]
        class MyCommand : PipelineCommand
        {
            protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
            {
                yield break;
            }
        }
    }
}