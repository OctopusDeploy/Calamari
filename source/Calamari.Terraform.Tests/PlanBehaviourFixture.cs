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
    // Exercises PlanBehaviour.Execute() against a mocked ICommandLineRunner. Terraform's own "plan"
    // exit-code contract (0 = no changes, 2 = changes pending, anything else = real error) is not what's
    // under test here - that's Terraform's behaviour, not Calamari's. What's under test is Calamari's
    // handling of that contract: does exit code 2 get treated as success rather than a failure, and does
    // it get captured into the TerraformPlanDetailedExitCode output variable correctly.
    public class PlanBehaviourFixture
    {
        IVariables variables;
        ILog log;
        ICommandLineRunner commandLineRunner;

        [SetUp]
        public void SetUp()
        {
            variables = Substitute.For<IVariables>();
            variables.GetStrings(KnownVariables.EnabledFeatureToggles).Returns(new List<string>());

            log = Substitute.For<ILog>();
            commandLineRunner = Substitute.For<ICommandLineRunner>();
        }

        void ConfigureCommandLineRunner(int planExitCode)
        {
            var callCount = 0;
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation =>
            {
                callCount++;
                if (callCount == 1)
                    invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v1.0.0");
            })).Returns(_ => callCount == 3 ? new CommandResult("terraform plan", planExitCode) : new CommandResult("terraform", 0));
        }

        PlanBehaviour CreateBehaviour() => new PlanBehaviour(log, Substitute.For<ICalamariFileSystem>(), commandLineRunner);

        [Test]
        public async Task Execute_ChangesPending_ExitCode2_DoesNotThrow_AndCapturesDetailedExitCode()
        {
            ConfigureCommandLineRunner(planExitCode: 2);

            await CreateBehaviour().Execute(new RunningDeployment("blah", variables));

            log.Received(1).SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode, "2", variables);
        }

        [Test]
        public async Task Execute_NoChanges_ExitCode0_CapturesDetailedExitCode()
        {
            ConfigureCommandLineRunner(planExitCode: 0);

            await CreateBehaviour().Execute(new RunningDeployment("blah", variables));

            log.Received(1).SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode, "0", variables);
        }

        [Test]
        public async Task Execute_RealError_ExitCode1_Throws()
        {
            ConfigureCommandLineRunner(planExitCode: 1);

            var act = () => CreateBehaviour().Execute(new RunningDeployment("blah", variables));

            await act.Should().ThrowAsync<CommandLineException>();
        }
    }
}
