using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class PackagedScriptConventionFixture
    {
        ICalamariFileSystem fileSystem;
        IScriptEngine scriptEngine;
        ICommandLineRunner runner;
        RunningDeployment deployment;
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
            scriptEngine = Substitute.For<IScriptEngine>();
            scriptEngine.Execute(Arg.Any<Script>(), Arg.Any<CalamariVariableDictionary>(), Arg.Any<ICommandLineRunner>()).Returns(c => commandResult);
            scriptEngine.GetSupportedTypes().Returns(new[] {ScriptType.ScriptCS, ScriptType.Powershell});
            runner = Substitute.For<ICommandLineRunner>();
            deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("Packages"), new CalamariVariableDictionary());
        }

        [Test]
        public void ShouldFindAndCallPackagedScripts()
        {
            var convention = CreateConvention("Deploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1")), deployment.Variables, runner);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx")), deployment.Variables, runner);
        }

        [Test]
        public void ShouldFindAndCallPreDeployScripts()
        {
            var convention = CreateConvention("PreDeploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1")), deployment.Variables, runner);
        }

        [Test]
        public void ShouldDeleteScriptAfterExecution()
        {
            var convention = CreateConvention("PreDeploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1")), deployment.Variables, runner);
            fileSystem.Received().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1"), Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldDeleteScriptAfterCleanupExecution()
        {
            var convention = CreateRollbackConvention("DeployFailed");
            convention.Cleanup(deployment);
            fileSystem.Received().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1"), Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldRunScriptOnRollbackExecution()
        {
            var convention = CreateRollbackConvention("DeployFailed");
            convention.Rollback(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1")), deployment.Variables, runner);
        }

        [Test]
        public void ShouldNotDeleteDeployFailedScriptAfterExecutionIfSpecialVariableIsSet()
        {
            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateRollbackConvention("DeployFailed");
            convention.Cleanup(deployment);
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1"), Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldNotDeletePreDeployScriptAfterExecutionIfSpecialVariableIsSet()
        {
            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateConvention("PreDeploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1")), deployment.Variables, runner);
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1"), Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldNotDeleteDeployScriptAfterExecutionIfSpecialVariableIsSet()
        {
            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateConvention("Deploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1")), deployment.Variables, runner);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx")), deployment.Variables, runner);
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1"), Arg.Any<FailureOptions>());
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx"), Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldNotDeletePostDeployScriptAfterExecutionIfSpecialVariableIsSet()
        {
            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateConvention("PostDeploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.ps1")), deployment.Variables, runner);
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.ps1"), Arg.Any<FailureOptions>());
        }

        PackagedScriptConvention CreateConvention(string scriptName)
        {
            return new PackagedScriptConvention(scriptName, fileSystem, scriptEngine, runner);
        }

        RollbackScriptConvention CreateRollbackConvention(string scriptName)
        {
            return new RollbackScriptConvention(scriptName, fileSystem, scriptEngine, runner);
        }
    }
}
