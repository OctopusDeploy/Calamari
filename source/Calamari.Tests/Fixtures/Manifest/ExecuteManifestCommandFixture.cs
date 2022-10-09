using System;
using System.IO;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment;
using Calamari.LaunchTools;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Manifest
{
    [TestFixture]
    [RequiresDotNetCore]
    public class ExecuteManifestCommandFixture : CalamariFixture
    {
        [Test]
        public void NoManifestFile()
        {
            var variables = new VariableDictionary();

            var result = ExecuteCommand(variables);

            result.AssertFailure();
            result.AssertErrorOutput("Execution manifest not found in variables.");
        }

        [Test]
        public void NoInstructions()
        {
            var variables = new VariableDictionary
            {
                { SpecialVariables.Execution.Manifest, "[]" }
            };

            var result = ExecuteCommand(variables);

            result.AssertFailure();
            result.AssertErrorOutput("The execution manifest must have at least one instruction.");
        }

        [Test]
        public void WithInstructions()
        {
            var instructions =
                InstructionBuilder
                    .Create()
                    .WithCalamariInstruction("test-calamari-instruction", new { Greeting = "Hello" })
                    .WithNodeInstruction()
                    .AsString();

            using (var temporaryDirectory = TemporaryDirectory.Create())
            {
                var generatedApplicationPath = CodeGenerator.GenerateConsoleApplication("node", temporaryDirectory.DirectoryPath);
                var toolRoot = Path.Combine(temporaryDirectory.DirectoryPath, "app");
                var destinationPath =
                    CalamariEnvironment.IsRunningOnWindows ? toolRoot : Path.Combine(toolRoot, "bin");

                DirectoryEx.Copy(generatedApplicationPath, destinationPath);

                var variables = new VariableDictionary
                {
                    { SpecialVariables.Execution.Manifest, instructions },
                    { nameof(NodeInstructions.BootstrapperPathVariable), "BootstrapperPathVariable_Value" },
                    { nameof(NodeInstructions.NodePathVariable), toolRoot },
                    { nameof(NodeInstructions.TargetPathVariable), "TargetPathVariable_Value" },
                    { nameof(NodeInstructions.InputsVariable), "no_empty" },
                    { nameof(NodeInstructions.DeploymentTargetInputsVariable), "deploymentTargetInputs" },
                };

                var result = ExecuteCommand(variables, "Calamari.Tests");

                result.AssertSuccess();
                result.AssertOutput("Hello from TestCommand");
                result.AssertOutput("Hello from my custom node!");
                result.AssertOutput(Path.Combine("BootstrapperPathVariable_Value", "bootstrapper.js"));
                result.AssertOutput("TargetPathVariable_Value");
                result.AssertOutput(nameof(NodeInstructions.DeploymentTargetInputsVariable));
            }
        }

        CalamariResult ExecuteCommand(VariableDictionary variables, string extensions = "")
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                variables.Save(variablesFile.FilePath);

                return Invoke(Calamari()
                              .Action("execute-manifest")
                              .Argument("variables", variablesFile.FilePath)
                              .Argument("sensitiveVariablesPassword", "GB8KdBqYRlgAON9ISUPdnQ==")
                              .Argument("extensions", extensions));
            }
        }
    }

    [Command("test-calamari-instruction")]
    public class TestCommand : Command<TestCommandInputs>
    {
        readonly ILog log;

        public TestCommand(ILog log)
        {
            this.log = log;
        }

        protected override void Execute(TestCommandInputs inputs)
        {
            log.Info($"{inputs.Greeting} from TestCommand");
        }
    }

    public class TestCommandInputs
    {
        public string Greeting { get; set; }
    }
}