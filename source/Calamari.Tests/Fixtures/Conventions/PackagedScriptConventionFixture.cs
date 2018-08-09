using Calamari.Commands;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Scripting;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;
using Script = Calamari.Shared.Scripting.Script;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class PackagedScriptConventionFixture
    {
        ICalamariFileSystem fileSystem;
        IScriptRunner scriptEngine;
        ICommandLineRunner runner;
        IExecutionContext deployment;
        CommandResult commandResult;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<string[]>()).Returns(new[]
            {
                TestEnvironment.ConstructRootedPath("App", "MyApp", "Hello.ps1"),
                TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1"),
                TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx"),
                TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1"),
                TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.ps1"),
                TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1")
            });

            commandResult = new CommandResult("PowerShell.exe foo bar", 0, null);
            scriptEngine = Substitute.For<IScriptRunner>();
            scriptEngine.Execute(Arg.Any<Script>()).Returns(c => commandResult);
            scriptEngine.GetSupportedTypes().Returns(new[] {ScriptSyntax.CSharp, ScriptSyntax.PowerShell});
            runner = Substitute.For<ICommandLineRunner>();
            deployment = new CalamariExecutionContext(TestEnvironment.ConstructRootedPath("Packages"),
                new CalamariVariableDictionary());
        }

        [Test]
        public void ShouldFindAndCallPackagedScripts()
        {
            var convention = CreateConvention("Deploy");
            convention.Run(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s =>
                s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1")));
            scriptEngine.Received().Execute(Arg.Is<Script>(s =>
                s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx")));
        }

        [Test]
        public void ShouldFindAndCallPreDeployScripts()
        {
            var convention = CreateConvention("PreDeploy");
            convention.Run(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s =>
                s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1")));
        }

        [Test]
        public void ShouldDeleteScriptAfterExecution()
        {
            var convention = CreateConvention("PreDeploy");
            convention.Run(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s =>
                s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1")));
            fileSystem.Received().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1"),
                Arg.Any<FailureOptions>());
        }


        [Test]
        public void ShouldDeleteScriptAfterCleanupExecution()
        {
            var convention = CreateRollbackConvention("DeployFailed");
            convention.Cleanup(deployment);
            fileSystem.Received().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1"),
                Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldRunScriptOnRollbackExecution()
        {
            var convention = CreateRollbackConvention("DeployFailed");
            convention.Run(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s =>
                s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1")));
        }

        [Test]
        public void ShouldNotDeleteDeployFailedScriptAfterExecutionIfSpecialVariableIsSet()
        {
            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateRollbackConvention("DeployFailed");
            convention.Cleanup(deployment);
            fileSystem.DidNotReceive()
                .DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1"),
                    Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldNotDeletePreDeployScriptAfterExecutionIfSpecialVariableIsSet()
        {
            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateConvention("PreDeploy");
            convention.Run(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s =>
                s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1")));
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1"),
                Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldNotDeleteDeployScriptAfterExecutionIfSpecialVariableIsSet()
        {
            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateConvention("Deploy");
            convention.Run(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s =>
                s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1")));
            scriptEngine.Received().Execute(Arg.Is<Script>(s =>
                s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx")));
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1"),
                Arg.Any<FailureOptions>());
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx"),
                Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldNotDeletePostDeployScriptAfterExecutionIfSpecialVariableIsSet()
        {
            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateConvention("PostDeploy");
            convention.Run(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s =>
                s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.ps1")));
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.ps1"),
                Arg.Any<FailureOptions>());
        }

        PackagedScriptConvention CreateConvention(string scriptName)
        {
            return new PackagedScriptConvention(scriptName, fileSystem, scriptEngine);
        }

        RollbackScriptConvention CreateRollbackConvention(string scriptName)
        {
            return new RollbackScriptConvention(scriptName, fileSystem, scriptEngine);
        }
    }
}
