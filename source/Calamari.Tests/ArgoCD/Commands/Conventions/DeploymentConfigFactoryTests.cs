#if NET
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
                [SpecialVariables.Git.Recursive] = "True",
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
                [SpecialVariables.Git.Recursive] = "True",
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
                [SpecialVariables.Git.Recursive] = "True",
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
    }
}
#endif