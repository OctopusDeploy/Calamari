using System;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Conventions
{
    [TestFixture]
    public class PackagedScriptConventionFixture
    {
        ICalamariFileSystem fileSystem;
        IScriptEngine scriptEngine;
        ICommandLineRunner runner;
        RunningDeployment deployment;
        CommandResult commandResult;
        InMemoryLog log;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<string[]>()).Returns(new[] {TestEnvironment.ConstructRootedPath("App", "MyApp", "Hello.ps1"), TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1"), TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx"), TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1"), TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.sh"), TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.ps1"), TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.sh"), TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1"), TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.sh")});

            commandResult = new CommandResult("PowerShell.exe foo bar", 0, null);
            scriptEngine = Substitute.For<IScriptEngine>();
            scriptEngine.Execute(Arg.Any<Script>(), Arg.Any<IVariables>(), Arg.Any<ICommandLineRunner>()).Returns(c => commandResult);
            scriptEngine.GetSupportedTypes().Returns(new[] {ScriptSyntax.CSharp, ScriptSyntax.PowerShell, ScriptSyntax.Bash});
            runner = Substitute.For<ICommandLineRunner>();
            deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("Packages"), new CalamariVariables());
            log = new InMemoryLog();
        }

        [Test]
        public void ShouldFindAndCallPreferredPackageScript()
        {
            var deployPs1 = TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1");
            var deployCsx = TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx");

            var convention = CreateConvention("Deploy");
            convention.Install(deployment);
            scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == deployPs1), deployment.Variables, runner);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == deployCsx), deployment.Variables, runner);
            log.StandardOut.Should().ContainMatch($"Found 2 Deploy scripts. Selected {deployCsx} based on OS preferential ordering: CSharp, PowerShell, Bash");
        }

        [Test]
        public void ShouldFindAndCallPreferredPreDeployScript()
        {
            var preDeployPs1 = TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1");
            var preDeploySh = TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.sh");

            var convention = CreateConvention("PreDeploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == preDeployPs1), deployment.Variables, runner);
            scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == preDeploySh), deployment.Variables, runner);
            log.StandardOut.Should().ContainMatch($"Found 2 PreDeploy scripts. Selected {preDeployPs1} based on OS preferential ordering: CSharp, PowerShell, Bash");
        }

        [Test]
        public void ShouldDeleteScriptsAfterExecution()
        {
            var preDeployPs1 = TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1");
            var preDeploySh = TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.sh");

            var convention = CreateConvention("PreDeploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == preDeployPs1), deployment.Variables, runner);
            fileSystem.Received().DeleteFile(preDeployPs1, Arg.Any<FailureOptions>());
            fileSystem.Received().DeleteFile(preDeploySh, Arg.Any<FailureOptions>());
            log.StandardOut.Should().ContainMatch($"Found 2 PreDeploy scripts. Selected {preDeployPs1} based on OS preferential ordering: CSharp, PowerShell, Bash");
        }

        [Test]
        public void ShouldDeleteScriptsAfterCleanupExecution()
        {
            var convention = CreateRollbackConvention("DeployFailed");
            convention.Cleanup(deployment);
            fileSystem.Received().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1"), Arg.Any<FailureOptions>());
            fileSystem.Received().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.sh"), Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldRunPreferredScriptOnRollbackExecution()
        {
            var deployFailedPs1 = TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.ps1");
            var deployFailedSh = TestEnvironment.ConstructRootedPath("App", "MyApp", "DeployFailed.sh");

            var convention = CreateRollbackConvention("DeployFailed");
            convention.Rollback(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == deployFailedPs1), deployment.Variables, runner);
            scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == deployFailedSh), deployment.Variables, runner);
            log.StandardOut.Should().ContainMatch($"Found 2 DeployFailed scripts. Selected {deployFailedPs1} based on OS preferential ordering: CSharp, PowerShell, Bash");
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
            var preDeployPs1 = TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1");
            var preDeploySh = TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.sh");

            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateConvention("PreDeploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == preDeployPs1), deployment.Variables, runner);
            scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == preDeploySh), deployment.Variables, runner);
            fileSystem.DidNotReceive().DeleteFile(preDeployPs1, Arg.Any<FailureOptions>());
            fileSystem.DidNotReceive().DeleteFile(preDeploySh, Arg.Any<FailureOptions>());
            log.StandardOut.Should().ContainMatch($"Found 2 PreDeploy scripts. Selected {preDeployPs1} based on OS preferential ordering: CSharp, PowerShell, Bash");
        }

        [Test]
        public void ShouldNotDeleteDeployScriptAfterExecutionIfSpecialVariableIsSet()
        {
            var deployCsx = TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx");
            var deployPs1 = TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1");

            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateConvention("Deploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == deployCsx), deployment.Variables, runner);
            scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == deployPs1), deployment.Variables, runner);
            fileSystem.DidNotReceive().DeleteFile(deployPs1, Arg.Any<FailureOptions>());
            fileSystem.DidNotReceive().DeleteFile(deployCsx, Arg.Any<FailureOptions>());
            log.StandardOut.Should().ContainMatch($"Found 2 Deploy scripts. Selected {deployCsx} based on OS preferential ordering: CSharp, PowerShell, Bash");
        }

        [Test]
        public void ShouldNotDeletePostDeployScriptAfterExecutionIfSpecialVariableIsSet()
        {
            var postDeployPs1 = TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.ps1");
            var postDeploySh = TestEnvironment.ConstructRootedPath("App", "MyApp", "PostDeploy.sh");

            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            var convention = CreateConvention("PostDeploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == postDeployPs1), deployment.Variables, runner);
            scriptEngine.DidNotReceive().Execute(Arg.Is<Script>(s => s.File == postDeploySh), deployment.Variables, runner);
            fileSystem.DidNotReceive().DeleteFile(postDeployPs1, Arg.Any<FailureOptions>());
            fileSystem.DidNotReceive().DeleteFile(postDeploySh, Arg.Any<FailureOptions>());
            log.StandardOut.Should().ContainMatch($"Found 2 PostDeploy scripts. Selected {postDeployPs1} based on OS preferential ordering: CSharp, PowerShell, Bash");
        }

        PackagedScriptConvention CreateConvention(string scriptName)
        {
            PackagedScriptBehaviour scriptBehaviour = null;
            if (scriptName == DeploymentStages.PreDeploy)
                scriptBehaviour = new PreDeployPackagedScriptBehaviour(log, fileSystem, scriptEngine, runner);
            else if (scriptName == DeploymentStages.Deploy)
                scriptBehaviour = new DeployPackagedScriptBehaviour(log, fileSystem, scriptEngine, runner);
            else if (scriptName == DeploymentStages.PostDeploy)
                scriptBehaviour = new PostDeployPackagedScriptBehaviour(log, fileSystem, scriptEngine, runner);

            return new PackagedScriptConvention(scriptBehaviour);
        }

        RollbackScriptConvention CreateRollbackConvention(string scriptName)
        {
            return new RollbackScriptConvention(log, scriptName, fileSystem, scriptEngine, runner);
        }
    }
}