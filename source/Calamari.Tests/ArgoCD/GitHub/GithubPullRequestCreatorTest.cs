using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Testing.Helpers;

namespace Calamari.Tests.ArgoCD.GitHub
{
    //Reintroduce this if you want to test the actual GH PR creation
    //[TestFixture]
    public class GithubPullRequestCreatorTest
    {
        InMemoryLog log = new InMemoryLog();
        
        //[Test]
        public async Task CreatePrInRepo()
        {
            GitHubClientFactory gitHubClientFactory = new GitHubClientFactory();
            GitHubPullRequestCreator prCreator = new GitHubPullRequestCreator(gitHubClientFactory);

            var connection = new GitConnection(
                                              "yourGithubUsername",
                                              "ADD_PAT_HERE",
                                              new Uri("https://github.com/rain-on/PopulatedArgoCd.git"),
                                              GitBranchName.CreateFromFriendlyName("NOT_USED_IN_THIS_TEST"));

            await prCreator.CreatePullRequest(log,
                                        connection,
                                        "The Title",
                                        "The body",
                                        GitBranchName.CreateFromFriendlyName("blork"),
                                        GitBranchName.CreateFromFriendlyName("main"),
                                        CancellationToken.None);
        }
    }
}