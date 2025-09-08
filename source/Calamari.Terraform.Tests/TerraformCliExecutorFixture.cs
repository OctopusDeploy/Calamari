using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Terraform.Tests
{
    public class TerraformCliExecutorFixture
    {
        IVariables variables;
        TerraformCliExecutor cliExecutor;

        [SetUp]
        public void Setup()
        {
            variables = Substitute.For<IVariables>();
            var commandLineRunner = Substitute.For<ICommandLineRunner>();
            commandLineRunner.Execute(Arg.Do<CommandLineInvocation>(invocation => invocation.AdditionalInvocationOutputSink.WriteInfo("Terraform v0.15.0")))
                             .Returns(new CommandResult("foo", 0));
            cliExecutor = new TerraformCliExecutor(Substitute.For<ILog>(),
                                                   Substitute.For<ICalamariFileSystem>(),
                                                   commandLineRunner,
                                                   new RunningDeployment("blah", variables),
                                                   new Dictionary<string, string>());
        }

        [Test]
        public void TerraformVariableFiles_Null()
        {
            variables.Get(TerraformSpecialVariables.Action.Terraform.VarFiles).Returns((string)null);
            cliExecutor.TerraformVariableFiles.Should().BeNull();
        }

        [Test]
        public void TerraformVariableFiles_SingleLine()
        {
            variables.Get(TerraformSpecialVariables.Action.Terraform.VarFiles).Returns("foo");
            cliExecutor.TerraformVariableFiles.Should().Be("-var-file=\"foo\"");
        }

        [Test]
        public void TerraformVariableFiles_MultiLine()
        {
            variables.Get(TerraformSpecialVariables.Action.Terraform.VarFiles).Returns("foo\nbar\r\nbaz");
            cliExecutor.TerraformVariableFiles.Should().Be("-var-file=\"foo\" -var-file=\"bar\" -var-file=\"baz\"");
        }
    }
}