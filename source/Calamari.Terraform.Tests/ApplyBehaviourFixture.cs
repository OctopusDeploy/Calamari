using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Terraform.Behaviours;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Terraform.Tests
{
    // Exercises ApplyBehaviour.Execute() against a mocked ICommandLineRunner - no real terraform binary
    // involved. TerraformCliExecutor always issues "version --json" then "init ..." on construction, so
    // captured argument index 2 is the "apply" command and index 3 is the "output -json" command.
    public class ApplyBehaviourFixture
    {
        IVariables variables;
        ILog log;
        ICommandLineRunner commandLineRunner;
        List<string> capturedArguments;

        [SetUp]
        public void SetUp()
        {
            variables = Substitute.For<IVariables>();
            variables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());

            log = Substitute.For<ILog>();
            commandLineRunner = Substitute.For<ICommandLineRunner>();
            capturedArguments = new List<string>();
        }

        void ConfigureCommandLineRunner(System.Func<int, string> outputFor = null, System.Func<int, CommandResult> resultFor = null)
        {
            outputFor ??= _ => null;
            resultFor ??= _ => new CommandResult("terraform", 0);

            var callCount = 0;
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation =>
            {
                callCount++;
                capturedArguments.Add(invocation.Arguments);
                var output = callCount == 1 ? "Terraform v1.0.0" : outputFor(callCount);
                if (output != null)
                    invocation.AdditionalInvocationOutputSink.WriteInfo(output);
            })).Returns(_ => resultFor(callCount));
        }

        ApplyBehaviour CreateBehaviour() => new ApplyBehaviour(log, Substitute.For<ICalamariFileSystem>(), commandLineRunner);

        [Test]
        public async Task Execute_ConstructsApplyArgs_WithVarFilesAndActionParams()
        {
            variables.Get(TerraformSpecialVariables.Action.Terraform.VarFiles).Returns("foo.tfvars");
            variables.Get(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams).Returns("-lock=false");
            ConfigureCommandLineRunner(resultFor: callCount => callCount == 4 ? new CommandResult("terraform output", 1) : new CommandResult("terraform", 0));

            await CreateBehaviour().Execute(new RunningDeployment("blah", variables));

            capturedArguments[2].Should().Contain("apply").And.Contain("-auto-approve").And.Contain("-var-file=\"foo.tfvars\"").And.Contain("-lock=false");
        }

        [Test]
        public async Task Execute_OutputCommandFails_DoesNotSetAnyOutputVariables()
        {
            ConfigureCommandLineRunner(resultFor: callCount => callCount == 4 ? new CommandResult("terraform output", 1) : new CommandResult("terraform", 0));

            await CreateBehaviour().Execute(new RunningDeployment("blah", variables));

            log.DidNotReceive().SetOutputVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IVariables>(), Arg.Any<bool>());
        }

        [Test]
        public async Task Execute_ParsesNonSensitiveOutputVariable()
        {
            const string outputJson = "{\"ami\":{\"value\":\"test-value\",\"type\":\"string\",\"sensitive\":false}}";
            ConfigureCommandLineRunner(outputFor: callCount => callCount == 4 ? outputJson : null);

            await CreateBehaviour().Execute(new RunningDeployment("blah", variables));

            log.Received(1).SetOutputVariable("TerraformValueOutputs[ami]", "test-value", variables, false);
        }

        [Test]
        public async Task Execute_ParsesSensitiveOutputVariable()
        {
            const string outputJson = "{\"password\":{\"value\":\"s3cr3t\",\"type\":\"string\",\"sensitive\":true}}";
            ConfigureCommandLineRunner(outputFor: callCount => callCount == 4 ? outputJson : null);

            await CreateBehaviour().Execute(new RunningDeployment("blah", variables));

            log.Received(1).SetOutputVariable("TerraformValueOutputs[password]", "s3cr3t", variables, true);
        }
    }
}
