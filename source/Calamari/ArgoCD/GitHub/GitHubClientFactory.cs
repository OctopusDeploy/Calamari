#nullable enable
using System;
using System.Net;
using System.Net.Http;
using Calamari.ArgoCD.Git;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using Octokit;
using Octokit.Internal;

namespace Calamari.ArgoCD.GitHub
{
    /// <summary>
    /// Creates GitHub clients for a given GitConnection
    /// </summary>
    public interface IGitHubClientFactory
    {
        IGitHubClient CreateGitHubClient(string username, string password);
        
        IGitHubClient CreateGitHubClient(Credentials? credential);

        IGitHubClient CreateGitHubClient(string token);
    }

    public class GitHubClientFactory : IGitHubClientFactory
    {
        IConnection CreateGitHubConnection()
        {
            var githubApiUrl = "https://api.github.com"; 
            var clientHandler = new HttpClientHandler
            {
#pragma warning disable DE0003
                Proxy = WebRequest.DefaultWebProxy
#pragma warning restore DE0003
            };

            return new Connection(
                new ProductHeaderValue("octopus-deploy"),
                new Uri(githubApiUrl),
                new InMemoryCredentialStore(Credentials.Anonymous), // Default defined in Connection
                new HttpClientAdapter(() => clientHandler),
                new SimpleJsonSerializer() // Default defined in Connection
            );
        }
        
        public IGitHubClient CreateGitHubClient(string token)
        {
            var credentials = new Credentials(token);
            return CreateGitHubClient(credentials);
        }
        
        public IGitHubClient CreateGitHubClient(string username, string password)
        {
            var credentials = new Credentials(username, password);
            return CreateGitHubClient(credentials);
        }
        
        public IGitHubClient CreateGitHubClient(Credentials? credentials)
        {
            var connection = CreateGitHubConnection();

            return new GitHubClient(connection)
            {
                Credentials = credentials
            };
        }
    }
}
