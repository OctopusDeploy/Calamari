using System;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.CommitToGit;
using Calamari.Deployment;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Calamari.Contracts.CommitToGit;

namespace Calamari.Tests.CommitToGit;

[TestFixture]
public class CommitToGitConfigFactoryTests
{
    INonSensitiveVariables nonSensitiveVariables;
    IVariables variables;
    ICustomPropertiesLoader loader;
    CommitToGitConfigFactory factory;

    [SetUp]
    public void SetUp()
    {
        nonSensitiveVariables = Substitute.For<INonSensitiveVariables>();
        variables = Substitute.For<IVariables>();
        loader = Substitute.For<ICustomPropertiesLoader>();
        factory = new CommitToGitConfigFactory(nonSensitiveVariables);

        variables.Get(SpecialVariables.Action.Git.Url).Returns("https://example.invalid/repo.git");
        variables.Get(SpecialVariables.Action.Git.Reference).Returns("refs/heads/main");
        nonSensitiveVariables.GetMandatoryVariableRaw(SpecialVariables.Action.Git.CommitMessageSummary)
                             .Returns("summary");
        nonSensitiveVariables.GetRaw(SpecialVariables.Action.Git.CommitMessageDescription)
                             .Returns(string.Empty);
    }

    [Test]
    public void CreateRepositoryConfig_UsesUsernameAndPasswordFromLoadedProperties()
    {
        loader.Load<CommitToGitCustomPropertiesDto>()
              .Returns(new CommitToGitCustomPropertiesDto("MyCred", "https://example.invalid/repo.git", "user-from-file", "pwd-from-file"));

        var deployment = new RunningDeployment(null, variables);

        var config = factory.CreateRepositoryConfig(deployment, loader);

        config.GitConnection.Username.Should().Be("user-from-file");
        config.GitConnection.Password.Should().Be("pwd-from-file");
        config.GitConnection.Url.Should().Be(new Uri("https://example.invalid/repo.git"));
    }
}
