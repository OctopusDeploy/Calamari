using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;
using Octostache;

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
                TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1")
            });

            commandResult = new CommandResult("PowerShell.exe foo bar", 0, null);
            scriptEngine = Substitute.For<IScriptEngine>();
            scriptEngine.Execute(Arg.Any<string>(), Arg.Any<VariableDictionary>(), Arg.Any<ICommandLineRunner>()).Returns(c => commandResult);
            scriptEngine.GetSupportedExtensions().Returns(new[] {"csx", "ps1"});
            runner = Substitute.For<ICommandLineRunner>();
            deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("Packages"), new VariableDictionary());
        }

        [Test]
        public void ShouldFindAndCallPackagedScripts()
        {
            var convention = CreateConvention("Deploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.ps1"), deployment.Variables, runner);
            scriptEngine.Received().Execute(TestEnvironment.ConstructRootedPath("App", "MyApp", "Deploy.csx"), deployment.Variables, runner);
        }

        [Test]
        public void ShouldFindAndCallPreDeployScripts()
        {
            var convention = CreateConvention("PreDeploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1"), deployment.Variables, runner);
        }

        [Test]
        public void ShouldDeleteScriptAfterExecution()
        {
            var convention = CreateConvention("PreDeploy");
            convention.Install(deployment);
            scriptEngine.Received().Execute(TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1"), deployment.Variables, runner);
            fileSystem.Received().DeleteFile(TestEnvironment.ConstructRootedPath("App", "MyApp", "PreDeploy.ps1"), Arg.Any<DeletionOptions>());
        }

        PackagedScriptConvention CreateConvention(string scriptName)
        {
            return new PackagedScriptConvention(scriptName, fileSystem, scriptEngine, runner);
        }
    }
}
