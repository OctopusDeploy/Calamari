using System.Threading;
using System.Threading.Tasks;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Testing.Helpers;
using Calamari.Tests.ArgoCD.Git;
using NUnit.Framework;

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

            var connection = new TestGitConnection(
                                              "yourGithubUsername",
                                              "ADD_PAT_HERE",
                                              "https://github.com/rain-on/PopulatedArgoCd.git",
                                              new GitBranchName("NOT_USED_IN_THIS_TEST"));

            await prCreator.CreatePullRequest(log,
                                        connection,
                                        "The Title",
                                        "The body",
                                        new GitBranchName("blork"),
                                        new GitBranchName("main"),
                                        CancellationToken.None);
        }
    }
}