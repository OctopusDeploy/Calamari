using System;
using Calamari.ArgoCD.Conventions;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    [TestFixture]
    public class DeploymentConfigFactoryTests
    {
        [Test]
        public void Create_CommitMessageFieldsDontReferenceSensitiveVariables_Evaluated()
        {
            var nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables()
            {
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Summary #{Foo}",
                [SpecialVariables.Git.CommitMessageDescription] = "Description #{Foo}",
                ["Foo"] = "Bar",
            };
            var allVariables = new CalamariVariables()
            {
                ["SuperSecret"] = "Shhhh!",                
            };
            allVariables.Merge(nonSensitiveCalamariVariables);

            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", allVariables);

            var factory = new DeploymentConfigFactory(nonSensitiveCalamariVariables);
            
            var config = factory.CreateCommitToGitConfig(runningDeployment);

            config.CommitParameters.Summary.Should().Be("Summary Bar");
            config.CommitParameters.Description.Should().Be("Description Bar");
        }
        
        [Test]
        public void Create_CommitMessageSummaryReferencesSensitiveVariables_ThrowsException()
        {
            var nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables()
            {
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Sh... #{SuperSecret}",
                ["Foo"] = "Bar",
            };
            var allVariables = new CalamariVariables()
            {
                ["SuperSecret"] = "Shhhh!",                
            };
            allVariables.Merge(nonSensitiveCalamariVariables);

            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", allVariables);

            var factory = new DeploymentConfigFactory(nonSensitiveCalamariVariables);
            
            Action action = () => factory.CreateCommitToGitConfig(runningDeployment);

            action.Should().Throw<CommandException>();
        }
        
        [Test]
        public void Create_CommitMessageDescriptionReferencesSensitiveVariables_ThrowsException()
        {
            var nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables()
            {
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Summary #{Foo}",
                [SpecialVariables.Git.CommitMessageDescription] = "Sh... #{SuperSecret}",
                ["Foo"] = "Bar",
            };
            var allVariables = new CalamariVariables()
            {
                ["SuperSecret"] = "Shhhh!",                
            };
            allVariables.Merge(nonSensitiveCalamariVariables);

            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", allVariables);

            var factory = new DeploymentConfigFactory(nonSensitiveCalamariVariables);
            
            Action action = () => factory.CreateCommitToGitConfig(runningDeployment);

            action.Should().Throw<CommandException>();
        }

        [TestCase(null, GitCommitParameters.DefaultPushRetryAttempts, TestName = "Unset uses the default")]
        [TestCase("5", 5, TestName = "Explicit value is honoured")]
        [TestCase("0", 0, TestName = "Minimum is honoured")]
        [TestCase("10", 10, TestName = "Maximum is honoured")]
        [TestCase("25", 10, TestName = "Above maximum is clamped down")]
        [TestCase("-5", 0, TestName = "Below minimum is clamped up")]
        public void Create_PushRetryAttempts_DefaultedAndClamped(string variableValue, int expected)
        {
            var nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables()
            {
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Summary",
            };
            var allVariables = new CalamariVariables();
            allVariables.Merge(nonSensitiveCalamariVariables);
            if (variableValue != null)
                allVariables[SpecialVariables.Git.PushRetryAttempts] = variableValue;

            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", allVariables);

            var factory = new DeploymentConfigFactory(nonSensitiveCalamariVariables);

            var config = factory.CreateCommitToGitConfig(runningDeployment);

            config.CommitParameters.PushRetryAttempts.Should().Be(expected);
        }
    }
}
