using System;
using System.Linq;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Git
{
    public interface IGitReferenceFactory
    {
        GitReference CreateGitReference(string reference);
    }
    
    public class GitReferenceFactory : IGitReferenceFactory
    {
        readonly IRepository repository;

        public GitReferenceFactory(IRepository repository)
        {
            this.repository = repository;
        }

        public GitReference CreateGitReference(string value)
        {
            if (value == GitHead.HeadAsTarget)
            {
                // we don't want to use "HEAD" so instead, use the canonical head  name
                return new GitBranchName(repository.Head.CanonicalName);
            }

            if (value.StartsWith(GitBranchName.Prefix))
            {
                return new GitBranchName(value);
            }

            if (value.StartsWith(GitTag.Prefix))
            {
                return new GitTag(value);
            }

            return CalculateReferenceType(value);
        }
        
        GitReference CalculateReferenceType(string value)
        {
            //Note: Git allows the same string to be used as the bare name for branch and tag
            // We have chosen to default to branch in this event.
            if (repository.Branches.Any(b => b.FriendlyName == value))
            {
                return GitBranchName.CreateFromFriendlyName(value);
            }
            
            if (repository.Tags.Any(t => t.FriendlyName == value))
            {
                return GitTag.CreateFromFriendlyName(value);
            }

            return new GitCommitSha(value);
        }
    }
}