#nullable enable
using System;
using System.Net;
using System.Net.Http;
using Calamari.ArgoCD.GitHub;
using Octokit;
using Octokit.Internal;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    public class GitHubApiAdapterFactory: IGitVendorApiAdapterFactory
    {
        bool CanInvokeWith(IRepositoryConnection repositoryConnection)
        {
            return GitHubRepositoryOwnerParser.IsGitHub(repositoryConnection.Url);
        }

        public IGitVendorApiAdapter? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection)
        {
            if (!CanInvokeWith(repositoryConnection))
            {
                return null;
            }
            
            var credentials = new Credentials(repositoryConnection.Username, repositoryConnection.Password);
            var client = CreateGitHubClient(credentials);
            return new GitHubApiAdapter(client, repositoryConnection, new Uri("https://github.com/"));
        }
        
        IGitHubClient CreateGitHubClient(Credentials? credentials)
        {
            var connection = CreateGitHubConnection();

            return new GitHubClient(connection)
            {
                Credentials = credentials
            };
        }
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
    }
}