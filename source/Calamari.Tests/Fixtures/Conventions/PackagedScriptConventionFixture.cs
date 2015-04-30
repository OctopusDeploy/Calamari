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
    [Category(TestEnvironment.CompatableOS.All)]
    public class PackagedScriptConventionFixture
    {
        ICalamariFileSystem fileSystem;
        IScriptEngineSelector selector;
        IScriptEngine engine;
        ICommandLineRunner runner;
        RunningDeployment deployment;
        CommandResult commandResult;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<string[]>()).Returns(new[]
            {
                "C:\\App\\MyApp\\Hello.ps1",
                "C:\\App\\MyApp\\Deploy.ps1",
                "C:\\App\\MyApp\\Deploy.csx",
                "C:\\App\\MyApp\\PreDeploy.ps1"
            });

            commandResult = new CommandResult("PowerShell.exe foo bar", 0, null);
            engine = Substitute.For<IScriptEngine>();
            engine.Execute(Arg.Any<string>(), Arg.Any<VariableDictionary>(), Arg.Any<ICommandLineRunner>()).Returns(c => commandResult);
            selector = Substitute.For<IScriptEngineSelector>();
            selector.GetSupportedExtensions().Returns(new[] {"csx", "ps1"});
            selector.SelectEngine(Arg.Any<string>()).Returns(engine);
            runner = Substitute.For<ICommandLineRunner>();
            deployment = new RunningDeployment("C:\\Packages", new VariableDictionary());
        }

        [Test]
        public void ShouldFindAndCallPackagedScripts()
        {
            var convention = CreateConvention("Deploy");
            convention.Install(deployment);
            engine.Received().Execute("C:\\App\\MyApp\\Deploy.ps1", deployment.Variables, runner);
            engine.Received().Execute("C:\\App\\MyApp\\Deploy.csx", deployment.Variables, runner);
        }

        [Test]
        public void ShouldFindAndCallPreDeployScripts()
        {
            var convention = CreateConvention("PreDeploy");
            convention.Install(deployment);
            engine.Received().Execute("C:\\App\\MyApp\\PreDeploy.ps1", deployment.Variables, runner);
        }

        [Test]
        public void ShouldDeleteScriptAfterExecution()
        {
            var convention = CreateConvention("PreDeploy");
            convention.Install(deployment);
            engine.Received().Execute("C:\\App\\MyApp\\PreDeploy.ps1", deployment.Variables, runner);
            fileSystem.Received().DeleteFile("C:\\App\\MyApp\\PreDeploy.ps1", Arg.Any<DeletionOptions>());
        }

        PackagedScriptConvention CreateConvention(string scriptName)
        {
            return new PackagedScriptConvention(scriptName, fileSystem, selector, runner);
        }
    }
}
