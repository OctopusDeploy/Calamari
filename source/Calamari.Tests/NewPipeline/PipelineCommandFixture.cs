using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common;
using Calamari.Common.Commands;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Tests.Fixtures;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.NewPipeline
{
    [TestFixture]
    [RequiresNonFreeBSDPlatform]
    [Category(TestCategory.PlatformAgnostic)]
    public class PipelineCommandFixture
    {
        readonly string[] defaultScriptStages = { DeploymentStages.PreDeploy, DeploymentStages.Deploy, DeploymentStages.PostDeploy };

        [Test]
        public Task EnsureAllContainerRegistrationsAreMet()
        {
            return CommandTestBuilder.CreateAsync<MyCommand, MyProgram>()
                              .Execute();
        }

        [Test]
        public Task AssertNextBehaviourIsNotCalledWhenPreviousBehaviourSetsSkipFlag()
        {
            return CommandTestBuilder.CreateAsync<SkipNextCommand, MyProgram>()
                                    .Execute();
        }

        [Test]
        public Task AssertPipelineExtensionsAreExecuted()
        {
            return CommandTestBuilder.CreateAsync<MyLoggingCommand, MyProgram>()
                                     .WithArrange(context =>
                                                  {
                                                      context.Variables.Add(ActionVariables.Name, "Boo");
                                                  })
                                    .WithAssert(result =>
                                                {
                                                    result.OutputVariables["ExecutionStages"].Value.Should().Be("IBeforePackageExtractionBehaviour;IAfterPackageExtractionBehaviour;IPreDeployBehaviour;IDeployBehaviour;IPostDeployBehaviour");
                                                })
                                    .Execute();
        }

        [Test]
        public async Task AssertStagedPackageIsExtracted()
        {
            using (var tempPath = TemporaryDirectory.Create())
            {
                File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "Hello.html"), "Hello World!");

                await CommandTestBuilder.CreateAsync<MyCommand, MyProgram>()
                                        .WithArrange(context =>
                                                     {
                                                         context.WithNewNugetPackage(tempPath.DirectoryPath, "MyPackage", "1.0.0");
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        File.Exists(Path.Combine(tempPath.DirectoryPath, "Hello.html")).Should().BeTrue();
                                                    })
                                        .Execute();
            }
        }

        [Test]
        public async Task AssertSubstituteInFilesRuns()
        {
            using (var tempPath = TemporaryDirectory.Create())
            {
                var targetPath = Path.Combine(tempPath.DirectoryPath, "myconfig.json");
                File.WriteAllText(targetPath, "{ foo: '#{Hello}' }");
                string glob = "**/*config.json";
                await CommandTestBuilder.CreateAsync<MyCommand, MyProgram>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.SubstituteInFiles);
                                                         context.Variables.Add(PackageVariables.SubstituteInFilesTargets, glob);
                                                         context.Variables.Add("Hello", "Hello World");
                                                         context.WithFilesToCopy(tempPath.DirectoryPath);
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        File.ReadAllText(targetPath).Should().Be("{ foo: 'Hello World' }");
                                                    })
                                        .Execute();
            }
        }

        [Test]
        public async Task AssertConfigurationTransformsRuns()
        {
            using (var tempPath = TemporaryDirectory.Create())
            {
                var expected = @"<configuration>
    <appSettings>
        <add key=""Environment"" value=""Test"" />
    </appSettings>
</configuration>".Replace("\r", String.Empty);
                var targetPath = Path.Combine(tempPath.DirectoryPath, "bar.config");
                File.WriteAllText(targetPath, @"<configuration>
    <appSettings>
        <add key=""Environment"" value=""Dev"" />
    </appSettings>
</configuration>");
                File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "bar.Release.config"), @"<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
    <appSettings>
        <add key=""Environment"" value=""Test"" xdt:Transform=""SetAttributes"" xdt:Locator=""Match(key)"" />
    </appSettings>
</configuration>");
                await CommandTestBuilder.CreateAsync<MyCommand, MyProgram>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.ConfigurationTransforms);
                                                         context.Variables.Add(KnownVariables.Package.AutomaticallyRunConfigurationTransformationFiles, bool.TrueString);
                                                         context.WithFilesToCopy(tempPath.DirectoryPath);
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        File.ReadAllText(targetPath).Replace("\r", String.Empty).Should().Be(expected);
                                                    })
                                        .Execute();
            }
        }

        [Test]
        public async Task AssertConfigurationVariablesRuns()
        {
            using (var tempPath = TemporaryDirectory.Create())
            {
                var expected = @"<configuration>
    <appSettings>
        <add key=""Environment"" value=""Test"" />
    </appSettings>
</configuration>".Replace("\r", String.Empty);
                var targetPath = Path.Combine(tempPath.DirectoryPath, "Web.config");
                File.WriteAllText(targetPath, @"<configuration>
    <appSettings>
        <add key=""Environment"" value=""Dev"" />
    </appSettings>
</configuration>");

                await CommandTestBuilder.CreateAsync<MyCommand, MyProgram>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.ConfigurationTransforms + "," + KnownVariables.Features.ConfigurationVariables);
                                                         context.Variables.Add(KnownVariables.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings, "true");
                                                         context.Variables.Add("Environment", "Test");
                                                         context.WithFilesToCopy(tempPath.DirectoryPath);
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        File.ReadAllText(targetPath).Replace("\r", String.Empty).Should().Be(expected);
                                                    })
                                        .Execute();
            }
        }

        [Test]
        public async Task AssertStructuredConfigurationVariablesRuns()
        {
            using (var tempPath = TemporaryDirectory.Create())
            {
                var expected = @"{
  ""Environment"": ""Test""
}".Replace("\r", String.Empty);
                var targetPath = Path.Combine(tempPath.DirectoryPath, "myfile.json");
                File.WriteAllText(targetPath, @"{
  ""Environment"": ""Dev""
}");

                await CommandTestBuilder.CreateAsync<MyCommand, MyProgram>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                                                         context.Variables.Add(ActionVariables.StructuredConfigurationVariablesTargets, "*.json");
                                                         context.Variables.Add("Environment", "Test");
                                                         context.WithFilesToCopy(tempPath.DirectoryPath);
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        File.ReadAllText(targetPath).Replace("\r", String.Empty).Should().Be(expected);
                                                    })
                                        .Execute();
            }
        }

        [Test]
        public async Task AssertInlineScriptsAreRun()
        {
            await CommandTestBuilder.CreateAsync<MyCommand, MyProgram>()
                                    .WithArrange(context =>
                                                 {
                                                     context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.CustomScripts);
                                                     foreach (var stage in defaultScriptStages)
                                                     {
                                                         context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(stage, ScriptSyntax.PowerShell), $"echo 'Hello from {stage}'");
                                                     }
                                                 })
                                    .WithAssert(result =>
                                                {
                                                    var currentIndex = 0;
                                                    foreach (var stage in defaultScriptStages)
                                                    {
                                                        var index = result.FullLog.IndexOf($"Hello from {stage}", StringComparison.Ordinal);
                                                        index.Should().BeGreaterThan(currentIndex);
                                                        currentIndex = index;
                                                    }
                                                })
                                    .Execute();
        }

        [Test]
        [TestCase(true, TestName = nameof(AssertPackageScriptsAreRun) + " and delete script after")]
        [TestCase(false, TestName = nameof(AssertPackageScriptsAreRun) + " and keep script after")]
        public async Task AssertPackageScriptsAreRun(bool deleteScripts)
        {
            using (var tempPath = TemporaryDirectory.Create())
            {
                foreach (var stage in defaultScriptStages)
                {
                    File.WriteAllText(Path.Combine(tempPath.DirectoryPath, $"{stage}.ps1"), $"echo 'Hello from {stage}'");
                }

                await CommandTestBuilder.CreateAsync<MyCommand, MyProgram>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(KnownVariables.Package.RunPackageScripts, bool.TrueString);
                                                         context.Variables.Add(KnownVariables.DeleteScriptsOnCleanup, deleteScripts.ToString());
                                                         context.WithFilesToCopy(tempPath.DirectoryPath);
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        var currentIndex = 0;
                                                        foreach (var stage in defaultScriptStages)
                                                        {
                                                            var index = result.FullLog.IndexOf($"Hello from {stage}", StringComparison.Ordinal);
                                                            index.Should().BeGreaterThan(currentIndex);
                                                            currentIndex = index;

                                                            File.Exists(Path.Combine(tempPath.DirectoryPath, $"{stage}.ps1")).Should().Be(!deleteScripts);
                                                        }
                                                    })
                                        .Execute();
            }
        }

        [TestCase(true, TestName = nameof(AssertDeployFailedScriptIsRunOnFailure) + " and delete script after")]
        [TestCase(false, TestName = nameof(AssertDeployFailedScriptIsRunOnFailure) + " and keep script after")]
        public async Task AssertDeployFailedScriptIsRunOnFailure(bool deleteScripts)
        {
            using (var tempPath = TemporaryDirectory.Create())
            {
                File.WriteAllText(Path.Combine(tempPath.DirectoryPath, $"{DeploymentStages.DeployFailed}.ps1"), $"echo 'Hello from {DeploymentStages.DeployFailed}'");

                await CommandTestBuilder.CreateAsync<MyBadCommand, MyProgram>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(KnownVariables.Package.RunPackageScripts, bool.TrueString);
                                                         context.Variables.Add(KnownVariables.DeleteScriptsOnCleanup, deleteScripts.ToString());
                                                         context.WithFilesToCopy(tempPath.DirectoryPath);
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        result.WasSuccessful.Should().BeFalse();

                                                        result.FullLog.Should().Contain($"Hello from {DeploymentStages.DeployFailed}");

                                                        File.Exists(Path.Combine(tempPath.DirectoryPath, $"{DeploymentStages.DeployFailed}.ps1")).Should().Be(!deleteScripts);
                                                    })
                                        .Execute(false);
            }
        }

        [Test]
        public async Task PackagedScriptDoesNotExecute()
        {
            using (var tempPath = TemporaryDirectory.Create())
            {
                foreach (var stage in defaultScriptStages)
                {
                    File.WriteAllText(Path.Combine(tempPath.DirectoryPath, $"{stage}.ps1"), $"echo 'Hello from {stage}'");
                }

                await CommandTestBuilder.CreateAsync<NoPackagedScriptCommand, MyProgram>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(KnownVariables.Package.RunPackageScripts, bool.TrueString);
                                                         context.WithFilesToCopy(tempPath.DirectoryPath);
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        result.WasSuccessful.Should().BeTrue();
                                                        foreach (var stage in defaultScriptStages)
                                                        {
                                                            result.FullLog.Should().NotContain($"Hello from {stage}");
                                                        }

                                                    })
                                        .Execute(true);
            }
        }

        [Test]
        public async Task ConfiguredScriptDoesNotExecute()
        {
            using (var tempPath = TemporaryDirectory.Create())
            {
                await CommandTestBuilder.CreateAsync<NoConfiguredScriptCommand, MyProgram>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(KnownVariables.Features.CustomScripts, bool.TrueString);
                                                         foreach (var stage in defaultScriptStages)
                                                         {
                                                             context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(stage, ScriptSyntax.PowerShell), $"echo 'Hello from {stage}");
                                                         }
                                                     })
                                        .WithAssert(result =>
                                                    {
                                                        result.WasSuccessful.Should().BeTrue();
                                                        foreach (var stage in defaultScriptStages)
                                                        {
                                                            result.FullLog.Should().NotContain($"Hello from {stage}");
                                                        }

                                                    })
                                        .Execute(true);
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
                yield return resolver.Create<MyEmptyBehaviour>();
            }
        }

        [Command("mybadcommand")]
        class MyBadCommand : PipelineCommand
        {
            protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
            {
                yield return resolver.Create<MyBadBehaviour>();
            }
        }

        [Command("skipcommand")]
        class SkipNextCommand : PipelineCommand
        {
            protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
            {
                yield return resolver.Create<SkipNextBehaviour>();
                yield return resolver.Create<MyBadBehaviour>();
            }
        }

        [Command("nopackagedscriptcommand")]
        class NoPackagedScriptCommand : PipelineCommand
        {
            protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
            {
                yield break;
            }

            protected override bool IncludePackagedScriptBehaviour => false;
        }

        [Command("noconfiguredscriptcommand")]
        class NoConfiguredScriptCommand : PipelineCommand
        {
            protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
            {
                yield break;
            }

            protected override bool IncludeConfiguredScriptBehaviour => false;
        }

        [Command("logcommand")]
        class MyLoggingCommand : PipelineCommand
        {
            protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
            {
                yield return resolver.Create<DeployBehaviour>();
            }

            protected override IEnumerable<IBeforePackageExtractionBehaviour> BeforePackageExtraction(BeforePackageExtractionResolver resolver)
            {
                yield return resolver.Create<BeforePackageExtractionBehaviour>();
            }

            protected override IEnumerable<IAfterPackageExtractionBehaviour> AfterPackageExtraction(AfterPackageExtractionResolver resolver)
            {
                yield return resolver.Create<AfterPackageExtractionBehaviour>();

            }

            protected override IEnumerable<IPreDeployBehaviour> PreDeploy(PreDeployResolver resolver)
            {
                yield return resolver.Create<PreDeployBehaviour>();

            }

            protected override IEnumerable<IPostDeployBehaviour> PostDeploy(PostDeployResolver resolver)
            {
                yield return resolver.Create<PostDeployBehaviour>();
            }
        }

        class SkipNextBehaviour : IDeployBehaviour
        {
            public bool IsEnabled(RunningDeployment context)
            {
                return true;
            }

            public Task Execute(RunningDeployment context)
            {
                context.Variables.AddFlag(KnownVariables.Action.SkipRemainingConventions, true);
                return this.CompletedTask();
            }
        }

        class MyBadBehaviour : IDeployBehaviour
        {
            public bool IsEnabled(RunningDeployment context)
            {
                return true;
            }

            public Task Execute(RunningDeployment context)
            {
                throw new Exception("Boom!!!");
            }
        }

        class MyEmptyBehaviour : IDeployBehaviour
        {
            public bool IsEnabled(RunningDeployment context)
            {
                return false;
            }

            public Task Execute(RunningDeployment context)
            {
                throw new NotImplementedException();
            }
        }

        class BeforePackageExtractionBehaviour : LoggingBehaviour, IBeforePackageExtractionBehaviour
        {
            public BeforePackageExtractionBehaviour(ILog log) : base(log)
            {
            }
        }

        class AfterPackageExtractionBehaviour : LoggingBehaviour, IAfterPackageExtractionBehaviour
        {
            public AfterPackageExtractionBehaviour(ILog log) : base(log)
            {
            }
        }

        class PreDeployBehaviour : LoggingBehaviour, IPreDeployBehaviour
        {
            public PreDeployBehaviour(ILog log) : base(log)
            {
            }
        }

        class DeployBehaviour : LoggingBehaviour, IDeployBehaviour
        {
            public DeployBehaviour(ILog log) : base(log)
            {
            }
        }

        class PostDeployBehaviour : LoggingBehaviour, IPostDeployBehaviour
        {
            public PostDeployBehaviour(ILog log) : base(log)
            {
            }
        }

        class LoggingBehaviour: IBehaviour
        {
            readonly ILog log;

            public LoggingBehaviour(ILog log)
            {
                this.log = log;
            }

            public bool IsEnabled(RunningDeployment context)
            {
                return true;
            }

            public Task Execute(RunningDeployment context)
            {
                var stages = context.Variables.GetStrings("ExecutionStages", ';');
                var name = GetType().FindInterfaces((type, criteria) => type != typeof(IBehaviour), "").Single().Name;
                stages.Add(name);
                log.SetOutputVariable("ExecutionStages", string.Join(";", stages), context.Variables);

                return this.CompletedTask();
            }
        }
    }
}