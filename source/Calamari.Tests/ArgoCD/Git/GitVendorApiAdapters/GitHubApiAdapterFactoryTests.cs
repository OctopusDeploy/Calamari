using System;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Git.GitVendorApiAdapters
{
    [TestFixture]
    public class GitHubApiAdapterFactoryTests
    {
        [Test]
        public void RandomHost_ReturnsNull()
        {
            var git = new GitHubApiAdapterFactory();
            var adapter = git.TryCreateGitVendorApiAdaptor(CreateConnection("http://someurl.com/org/repo"));
            adapter.Should().BeNull();
        }

        [Test]
        public void GitHubDomain_ReturnsAdapter()
        {
            var git = new GitHubApiAdapterFactory();
            var adapter = git.TryCreateGitVendorApiAdaptor(CreateConnection("http://github.com/org/repo"));
            adapter.Should().NotBeNull();
        }

        [Test]
        public void GitHubSubDomain_ReturnsAdapter()
        {
            var git = new GitHubApiAdapterFactory();
            var adapter = git.TryCreateGitVendorApiAdaptor(CreateConnection("http://foo.github.com/org/repo"));
            adapter.Should().NotBeNull();
        }

        [Test]
        public void NonGitHubSubDomain_ReturnsNull()
        {
            var git = new GitHubApiAdapterFactory();
            var adapter = git.TryCreateGitVendorApiAdaptor(CreateConnection("http://github.com.foobar/org/repo"));
            adapter.Should().BeNull();
        }

        GitConnection CreateConnection(string url)
        {
            return new GitConnection(Some.String(), Some.String(), new Uri(url), GitBranchName.CreateFromFriendlyName("main"));
        }
    }
}