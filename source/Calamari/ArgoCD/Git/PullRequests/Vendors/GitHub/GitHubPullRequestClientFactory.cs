#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Octokit;
using Octokit.Internal;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.GitHub
{
    public class GitHubPullRequestClientFactory: IGitVendorPullRequestClientFactory
    {
        public string Name => "GitHub";
        
        public bool CanHandleAsCloudHosted(Uri repositoryUri) => GitHubRepositoryUriParser.IsGitHub(repositoryUri);

        public async Task<IGitVendorPullRequestClient> Create(HttpsGitConnection repositoryConnection, ILog log, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            
            var credentials = new Credentials(repositoryConnection.Username, repositoryConnection.Password);
            var client = CreateGitHubClient(credentials);
            return new GitHubPullRequestClient(client, repositoryConnection, new Uri("https://github.com/"));
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