using System;
using Calamari.ArgoCD.Git;
using Calamari.Common.Commands;
using FluentAssertions;
using NUnit.Framework;
using ArgoCdPasswordCredential = Octopus.Calamari.Contracts.ArgoCD.GitCredentialDto;
using ArgoCdSshKeyCredential = Octopus.Calamari.Contracts.ArgoCD.SshKeyGitCredentialDto;
using GitCredential = Octopus.Calamari.Contracts.Git.IGitCredentialDto;
using GitPasswordCredential = Octopus.Calamari.Contracts.Git.UsernamePasswordGitCredentialDto;
using GitSshKeyCredential = Octopus.Calamari.Contracts.Git.SshKeyGitCredentialDto;

namespace Calamari.Tests.ArgoCD.Git;

[TestFixture]
public class GitConnectionFactoryTests
{
    static readonly GitReference Reference = GitBranchName.CreateFromFriendlyName("main");
    const string HttpsUrl = "https://github.com/org/repo.git";
    const string SshUrl = "ssh://git@github.com/org/repo.git";

    // The ArgoCD overload maps Argo credentials onto the common credential type and delegates to the
    // overload below, so we only validate the mapping here; the behaviour is covered by the common tests.

    [Test]
    public void ArgoCdSshKeyCredentialIsMappedToSshKeyGitConnection()
    {
        var connection = GitConnectionFactory.Create(new ArgoCdSshKeyCredential(SshUrl, "git", "private-key", []), SshUrl, Reference, createsPr: false);

        connection.Should().BeOfType<SshKeyGitConnection>();
    }

    [Test]
    public void ArgoCdUsernamePasswordCredentialIsMappedToUsernamePasswordGitConnection()
    {
        var connection = GitConnectionFactory.Create(new ArgoCdPasswordCredential(HttpsUrl, "user", "password"), HttpsUrl, Reference, createsPr: false);

        connection.Should().BeOfType<UsernamePasswordGitConnection>();
    }

    [Test]
    public void SshKeyCredentialThrowsWhenCreatingAPullRequest()
    {
        Action action = () => GitConnectionFactory.Create(new GitSshKeyCredential("cred", SshUrl, "git", "private-key", []), SshUrl, Reference, createsPr: true);

        action.Should().Throw<CommandException>().And.Message.Should().Contain("SSH key authentication");
    }

    [Test]
    public void SshKeyCredentialSucceedsWhenNotCreatingAPullRequest()
    {
        var connection = GitConnectionFactory.Create(new GitSshKeyCredential("cred", SshUrl, "git", "private-key", []), SshUrl, Reference, createsPr: false);

        connection.Should().BeOfType<SshKeyGitConnection>();
    }

    [Test]
    public void UsernamePasswordCredentialSucceedsWhenCreatingAPullRequest()
    {
        var connection = GitConnectionFactory.Create(new GitPasswordCredential("cred", HttpsUrl, "user", "password"), HttpsUrl, Reference, createsPr: true);

        connection.Should().BeOfType<UsernamePasswordGitConnection>();
    }

    [Test]
    public void NoCredentialThrowsWhenCreatingAPullRequest()
    {
        Action action = () => GitConnectionFactory.Create((GitCredential)null, HttpsUrl, Reference, createsPr: true);

        action.Should().Throw<CommandException>().And.Message.Should().Contain("requires Git repository credentials");
    }

    [Test]
    public void NoCredentialIsAnonymousWhenNotCreatingAPullRequest()
    {
        var connection = GitConnectionFactory.Create((GitCredential)null, HttpsUrl, Reference, createsPr: false);

        connection.Should().BeOfType<AnonymousGitConnection>();
    }
}
