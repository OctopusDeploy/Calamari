using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using Calamari.Variables;
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
                TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.sh"),
                TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.ps1"),
                TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.sh"),
                TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1"),
                TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.sh")
            });

            commandResult = new CommandResult("PowerShell.exe foo bar", 0, null);
            scriptEngine = Substitute.For<IScriptEngine>();
            scriptEngine.Execute(Arg.Any<Script>(), Arg.Any<IVariables>(), Arg.Any<ICommandLineRunner>()).Returns(c => commandResult);
            scriptEngine.GetSupportedTypes().Returns(new[] {ScriptSyntax.CSharp, ScriptSyntax.PowerShell, ScriptSyntax.Bash});
            runner = Substitute.For<ICommandLineRunner>();
            deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("Packages"), new CalamariVariables());
        }

        [Test]
        public void ShouldFindAndCallPreferredPackageScript()
        {
            using (var log = new ProxyLog())
            {
                var convention = CreateConvention("Deploy");
                convention.Install(deployment);
                scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1")), deployment.Variables, runner);
                scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx")), deployment.Variables, runner);
                log.AssertContains(@"Found 2 Deploy scripts. Selected C:\App\MyApp\Deploy.csx based on OS preferential ordering: CSharp -> PowerShell -> Bash");
            }
        }

        [Test]
        public void ShouldFindAndCallPreferredPreDeployScript()
        {
            using (var log = new ProxyLog())
            {
                var convention = CreateConvention("PreDeploy");
                convention.Install(deployment);
                scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1")), deployment.Variables, runner);
                scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.sh")), deployment.Variables, runner);
                log.AssertContains(@"Found 2 PreDeploy scripts. Selected C:\App\MyApp\PreDeploy.ps1 based on OS preferential ordering: CSharp -> PowerShell -> Bash");
            }
        }

        [Test]
        public void ShouldDeletePreferredScriptAfterExecution()
        {
            using (var log = new ProxyLog())
            {
                var convention = CreateConvention("PreDeploy");
                convention.Install(deployment);
                scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1")), deployment.Variables, runner);
                fileSystem.Received().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1"), Arg.Any<FailureOptions>());
                fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.sh"), Arg.Any<FailureOptions>());
                log.AssertContains(@"Found 2 PreDeploy scripts. Selected C:\App\MyApp\PreDeploy.ps1 based on OS preferential ordering: CSharp -> PowerShell -> Bash");
            }
        }

        [Test]
        public void ShouldDeletePreferredScriptAfterCleanupExecution()
        {
            var convention = CreateRollbackConvention("DeployFailed");
            convention.Cleanup(deployment);
            fileSystem.Received().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1"), Arg.Any<FailureOptions>());
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.sh"), Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldRunPreferredScriptOnRollbackExecution()
        {
            using (var log = new ProxyLog())
            {
                var convention = CreateRollbackConvention("DeployFailed");
                convention.Rollback(deployment);
                scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1")), deployment.Variables, runner);
                scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.sh")), deployment.Variables, runner);
                log.AssertContains(@"Found 2 DeployFailed scripts. Selected C:\App\MyApp\DeployFailed.ps1 based on OS preferential ordering: CSharp -> PowerShell -> Bash");
            }
        }

        [Test]
        public void ShouldNotDeleteDeployFailedScriptAfterExecutionIfSpecialVariableIsSet()
        {
            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateRollbackConvention("DeployFailed");
            convention.Cleanup(deployment);
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1"), Arg.Any<FailureOptions>());
            fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.sh"), Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldNotDeletePreDeployScriptAfterExecutionIfSpecialVariableIsSet()
        {
            using (var log = new ProxyLog())
            {
                deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
                var convention = CreateConvention("PreDeploy");
                convention.Install(deployment);
                scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1")), deployment.Variables, runner);
                scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.sh")), deployment.Variables, runner);
                fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1"), Arg.Any<FailureOptions>());
                fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.sh"), Arg.Any<FailureOptions>());
                log.AssertContains(@"Found 2 PreDeploy scripts. Selected C:\App\MyApp\PreDeploy.ps1 based on OS preferential ordering: CSharp -> PowerShell -> Bash");
            }
        }

        [Test]
        public void ShouldNotDeleteDeployScriptAfterExecutionIfSpecialVariableIsSet()
        {
            using (var log = new ProxyLog())
            {
                deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
                var convention = CreateConvention("Deploy");
                convention.Install(deployment);
                scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx")), deployment.Variables, runner);
                scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1")), deployment.Variables, runner);
                fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1"), Arg.Any<FailureOptions>());
                fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx"), Arg.Any<FailureOptions>());
                log.AssertContains(@"Found 2 Deploy scripts. Selected C:\App\MyApp\Deploy.csx based on OS preferential ordering: CSharp -> PowerShell -> Bash");
            }
        }

        [Test]
        public void ShouldNotDeletePostDeployScriptAfterExecutionIfSpecialVariableIsSet()
        {
            using (var log = new ProxyLog())
            {
                deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
                var convention = CreateConvention("PostDeploy");
                convention.Install(deployment);
                scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.ps1")), deployment.Variables, runner);
                scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.sh")), deployment.Variables, runner);
                fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.ps1"), Arg.Any<FailureOptions>());
                fileSystem.DidNotReceive().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.sh"), Arg.Any<FailureOptions>());
                log.AssertContains(@"Found 2 PostDeploy scripts. Selected C:\App\MyApp\PostDeploy.ps1 based on OS preferential ordering: CSharp -> PowerShell -> Bash");
            }
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
