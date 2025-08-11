using System;
using System.Collections.Generic;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Conventions
{
    public class GitInstallationContext
    {
        public void AddRepository(Repository respotiry)
        {
            Repositories.Add(respotiry);
        }

        public List<Repository> Repositories { get; } = new List<Repository>();
    }
}