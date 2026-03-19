using System;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using Calamari.ArgoCD.Git.GitVendorApiAdapters.GitHub;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git.GitVendorApiAdapters
{
    [TestFixture]
    public class GitHubApiAdapterFactoryTests
    {
        [Test]
        public void GitHubDomain_ReturnsAdapter()
        {
            var git = new GitHubApiAdapterFactory();
            var adapter = git.Create(CreateConnection("http://github.com/org/repo"));
            adapter.Should().NotBeNull();
        }

        [Test]
        public void GitHubSubDomain_ReturnsAdapter()
        {
            var git = new GitHubApiAdapterFactory();
            var adapter = git.Create(CreateConnection("http://foo.github.com/org/repo"));
            adapter.Should().NotBeNull();
        }

        GitConnection CreateConnection(string url)
        {
            return new GitConnection(Some.String(), Some.String(), new Uri(url), GitBranchName.CreateFromFriendlyName("main"));
        }
    }
}
