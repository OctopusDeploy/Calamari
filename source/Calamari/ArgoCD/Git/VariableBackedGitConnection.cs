#nullable enable
using System;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Conventions
{
    public class VariableBackedGitConnection : IGitConnection
    {
        IVariables variables;
        string index;

        public VariableBackedGitConnection(IVariables variables, string index)
        {
            this.variables = variables;
            this.index = index;
        }

        public string? Username => variables.Get(SpecialVariables.Git.Username(index));
        public string? Password => variables.Get(SpecialVariables.Git.Password(index));
        public string Url => variables.GetMandatoryVariable(SpecialVariables.Git.Url(index));
        public string BranchName => variables.GetMandatoryVariable(SpecialVariables.Git.BranchName(index));

        public string SubFolder
        {
            get
            {
                var raw = variables.Get(SpecialVariables.Git.SubFolder(index), String.Empty) ?? String.Empty;
                if (raw.StartsWith("./"))
                {
                    return raw.Substring(2);
                }

                return raw;
            }
        }
        public string RemoteBranchName => $"origin/{BranchName}";
    }

    public interface IGitConnection
    {
        public string? Username { get;  }
        public string? Password { get;  }
        public string Url { get;  }
        public string BranchName { get;  }
        
        public string SubFolder { get; }
        public string RemoteBranchName { get;  }
    }
}