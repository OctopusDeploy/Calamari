using System.Globalization;
using Calamari.ArgoCD.Git;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.CommitToGit
{
    public class CommitToGitOutputVariablesWriter
    {
        public const string CommitSha = "CommitToGit.CommitSha";
        public const string ShortSha = "CommitToGit.ShortSha";
        public const string CommitTimestamp = "CommitToGit.CommitTimestamp";
        public const string PullRequestTitle = "CommitToGit.PullRequest.Title";
        public const string PullRequestNumber = "CommitToGit.PullRequest.Number";
        public const string PullRequestUrl = "CommitToGit.PullRequest.Url";
        public const string PullRequestRepositoryUrl = "CommitToGit.PullRequest.RepositoryUrl";

        readonly ILog log;

        public CommitToGitOutputVariablesWriter(ILog log)
        {
            this.log = log;
        }

        public void WritePushResultOutput(PushResult pushResult)
        {
            log.SetOutputVariableButDoNotAddToVariables(CommitSha, pushResult.CommitSha);
            log.SetOutputVariableButDoNotAddToVariables(ShortSha, pushResult.ShortSha);
            log.SetOutputVariableButDoNotAddToVariables(CommitTimestamp, pushResult.CommitTimestamp.ToString("O"));

            if (pushResult is PullRequestPushResult prResult)
            {
                log.SetOutputVariableButDoNotAddToVariables(PullRequestTitle, prResult.PullRequestTitle);
                log.SetOutputVariableButDoNotAddToVariables(PullRequestNumber, prResult.PullRequestNumber.ToString(CultureInfo.InvariantCulture));
                log.SetOutputVariableButDoNotAddToVariables(PullRequestUrl, prResult.PullRequestUri);
                log.SetOutputVariableButDoNotAddToVariables(PullRequestRepositoryUrl, prResult.RepositoryUri);
            }
        }
    }
}
