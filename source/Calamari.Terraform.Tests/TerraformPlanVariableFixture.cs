using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Terraform.Behaviours;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Calamari.Terraform.Tests
{
    public class TerraformPlanVariableFixture
    {
        const string JsonPlanOutput = @"{""@level"":""info"",""@message"":""Terraform 1.0.4"",""@module"":""terraform.ui"",""@timestamp"":""2021-08-24T07:29:27.798620+10:00"",""terraform"":""1.0.4"",""type"":""version"",""ui"":""0.1.0""}
{""@level"":""info"",""@message"":""Plan: 0 to add, 0 to change, 0 to destroy."",""@module"":""terraform.ui"",""@timestamp"":""2021-08-24T07:29:27.884954+10:00"",""changes"":{""add"":0,""change"":0,""remove"":0,""operation"":""plan""},""type"":""change_summary""}";

        ILog log;
        ICalamariFileSystem calamariFileSystem;
        ICommandLineRunner commandLineRunner;
        RunningDeployment runningDeployment;
        IVariables variables;

        readonly IDictionary<string, string> outputVars = new Dictionary<string, string>();

        [SetUp]
        public void SetUp()
        {
            log = Substitute.For<ILog>();
            log.When(x => x.SetOutputVariable(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IVariables>()))
               .Do(x => outputVars.Add(x.ArgAt<string>(0), x.ArgAt<string>(1)));

            calamariFileSystem = Substitute.For<ICalamariFileSystem>();
            commandLineRunner = Substitute.For<ICommandLineRunner>();
            variables = new CalamariVariables();
            variables.Set(TerraformSpecialVariables.Action.Terraform.PlanJsonOutput, "True");
            runningDeployment = Substitute.For<RunningDeployment>(variables);
        }

        [TestCase("0.11", "")]
        [TestCase("0.11.5", "")]
        [TestCase("0.12", "--json")]
        [TestCase("1.0", "--json")]
        public void EnsurePlanOnlyWorksForTwelveAndAbove(string version, string expected)
        {
            new PlanBehaviour(log, calamariFileSystem, commandLineRunner).GetOutputParameter(runningDeployment, new Version(version)).ShouldBe(expected);
        }

        [Test]
        public void TestVariablesAreCaptured()
        {
            var splitOutput = Regex.Split(JsonPlanOutput, PlanBehaviour.LineEndingRE);
            new PlanBehaviour(log, calamariFileSystem, commandLineRunner).CaptureJsonOutput(runningDeployment, JsonPlanOutput);

            for (var index = 0; index < splitOutput.Length; ++index)
            {
                outputVars[$"TerraformPlanLine[{index}].JSON"].ShouldBe(splitOutput[index]);
            }

            outputVars[TerraformSpecialVariables.Action.Terraform.PlanJsonChangesAdd].ShouldBe("0");
            outputVars[TerraformSpecialVariables.Action.Terraform.PlanJsonChangesRemove].ShouldBe("0");
            outputVars[TerraformSpecialVariables.Action.Terraform.PlanJsonChangesChange].ShouldBe("0");
        }
    }
}