using System;
using Calamari.ArgoCD.GitHub;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.GitHub
{
    public class GitHubRepositoryOwnerParserTests
    {
        [Test]
        public void ParseOwnerAndRepository_ContainsGitExtension_ExtensionRemoved()
        {
            var (owner, repository) = GitHubRepositoryOwnerParser.ParseOwnerAndRepository(new Uri("https://github.com/OctopusDeploy/Calamari.git"));

            owner.Should().Be("OctopusDeploy");
            repository.Should().Be("Calamari");
        }
        
        [Test]
        public void ParseOwnerAndRepository_DoesntContainGitExtension_RepositoryReturned()
        {
            var (owner, repository) = GitHubRepositoryOwnerParser.ParseOwnerAndRepository(new Uri("https://github.com/OctopusDeploy/Calamari"));

            owner.Should().Be("OctopusDeploy");
            repository.Should().Be("Calamari");
        }
        
        [Test]
        public void ParseOwnerAndRepository_DomainContainsWWW_RepositoryReturned()
        {
            var (owner, repository) = GitHubRepositoryOwnerParser.ParseOwnerAndRepository(new Uri("https://www.github.com/OctopusDeploy/Calamari"));

            owner.Should().Be("OctopusDeploy");
            repository.Should().Be("Calamari");
        }
        
        [Theory]
        [TestCase("www.notgithub.com")]
        [TestCase("notgithub.com")]
        [TestCase("other.com")]
        [TestCase("github.com.au")]
        public void ParseOwnerAndRepository_NotGitHub_Throws(string host)
        {
            Action action = () => GitHubRepositoryOwnerParser.ParseOwnerAndRepository(new Uri($"https://{host}/OctopusDeploy/Calamari"));

            action.Should().Throw<InvalidOperationException>();
        }
        
        [Theory]
        [TestCase("Octopus/")]
        [TestCase("Octopus")]
        [TestCase("")]
        public void ParseOwnerAndRepository_DoesntContainOwnerAndRepository_Throws(string path)
        {
            Action action = () => GitHubRepositoryOwnerParser.ParseOwnerAndRepository(new Uri($"https://github/{path}"));

            action.Should().Throw<InvalidOperationException>();
        }
    }
}
