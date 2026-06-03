using Calamari.ArgoCD.Git;
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

    // The factory only maps a credential onto the matching connection type, carrying its fields across.
    // The HTTPS connection types expose a Lazy<Uri> derived from Url, so it is excluded from the
    // equivalence checks (Url itself is still asserted).

    // The ArgoCD overload maps Argo credentials onto the common credential type before delegating, so
    // we confirm each Argo credential lands on the right connection type with its fields mapped across.

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
        var connection = GitConnectionFactory.Create(new GitSshKeyCredential("cred", SshUrl, "git", "private-key", []), SshUrl, Reference);

        connection.Should().BeEquivalentTo(new SshKeyGitConnection("git", "private-key", SshUrl, Reference, []));
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
