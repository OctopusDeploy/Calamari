using Calamari.ArgoCD.Git;
using FluentAssertions;
using NUnit.Framework;
using ArgoCdPasswordCredential = Octopus.Calamari.Contracts.ArgoCD.GitCredentialDto;
using ArgoCdSshKeyCredential = Octopus.Calamari.Contracts.ArgoCD.SshKeyGitCredentialDto;
using GitCredential = Octopus.Calamari.Contracts.Git.IGitCredentialDto;
using GitPasswordCredential = Octopus.Calamari.Contracts.Git.UsernamePasswordGitCredentialDto;
using GitSshKeyCredential = Octopus.Calamari.Contracts.Git.SshKeyGitCredentialDto;
using GitSshKnownHostDto = Octopus.Calamari.Contracts.Git.SshKnownHostDto;

namespace Calamari.Tests.ArgoCD.Git;

[TestFixture]
public class GitConnectionFactoryTests
{
    static readonly GitReference Reference = GitBranchName.CreateFromFriendlyName("main");
    const string HttpsUrl = "https://github.com/org/repo.git";
    const string SshUrl = "ssh://git@github.com/org/repo.git";

    [Test]
    public void ArgoCdSshKeyCredentialIsMappedToSshKeyGitConnection()
    {
        var connection = GitConnectionFactory.Create(new ArgoCdSshKeyCredential(SshUrl, "git", "private-key", []), SshUrl, Reference);

        connection.Should().BeEquivalentTo(new SshKeyGitConnection("git", "private-key", SshUrl, Reference, []));
    }

    [Test]
    public void ArgoCdUsernamePasswordCredentialIsMappedToUsernamePasswordGitConnection()
    {
        var connection = GitConnectionFactory.Create(new ArgoCdPasswordCredential(HttpsUrl, "user", "password"), HttpsUrl, Reference);

        connection.Should().BeEquivalentTo(new UsernamePasswordGitConnection("user", "password", HttpsUrl, Reference), options => options.Excluding(c => c.Uri));
    }

    [Test]
    public void SshKeyCredentialIsMappedToSshKeyGitConnection()
    {
        GitSshKnownHostDto[] knownHosts =
        [
            new ("github.com", "AAAAB3NzaC1yc2EAAAADAQABAAABAQ=="),
            new ("bitbucket.org", "AAAAC3NzaC1lZDI1NTE5AAAAIA=="),
        ];
        var connection = GitConnectionFactory.Create(new GitSshKeyCredential("cred", SshUrl, "git", "private-key", knownHosts), SshUrl, Reference);


        SshKnownHost[] expectedKnownHosts =
        [
            new("github.com", "AAAAB3NzaC1yc2EAAAADAQABAAABAQ"),
            new("bitbucket.org", "AAAAC3NzaC1lZDI1NTE5AAAAIA=="),
        ];
        connection.Should().BeEquivalentTo(new SshKeyGitConnection("git", "private-key", SshUrl, Reference, expectedKnownHosts));
    }

    [Test]
    public void UsernamePasswordCredentialIsMappedToUsernamePasswordGitConnection()
    {
        var connection = GitConnectionFactory.Create(new GitPasswordCredential("cred", HttpsUrl, "user", "password"), HttpsUrl, Reference);

        connection.Should().BeEquivalentTo(new UsernamePasswordGitConnection("user", "password", HttpsUrl, Reference), options => options.Excluding(c => c.Uri));
    }

    [Test]
    public void NoCredentialIsMappedToAnonymousGitConnection()
    {
        var connection = GitConnectionFactory.Create((GitCredential)null, HttpsUrl, Reference);

        connection.Should().BeEquivalentTo(new AnonymousGitConnection(HttpsUrl, Reference), options => options.Excluding(c => c.Uri));
    }
}
