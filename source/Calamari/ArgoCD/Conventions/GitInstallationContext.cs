using System;
using System.Collections.Generic;
using LibGit2Sharp;

namespace Calamari.ArgoCD.Conventions
{
    public class GitInstallationContext
    {
        public void AddRepository(Repository repository)
        {
            Repositories.Add(repository);
        }

        public List<Repository> Repositories { get; } = new List<Repository>();
        
        public string WorkingDirectory { get; set; }
    }
}