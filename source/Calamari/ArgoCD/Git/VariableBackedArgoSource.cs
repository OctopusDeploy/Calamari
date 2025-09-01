#nullable enable
using System;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Git
{
    public class VariableBackedArgoSource : IArgoApplicationSource
    {
        IVariables variables;
        string index;

        public VariableBackedArgoSource(IVariables variables, string index)
        {
            this.variables = variables;
            this.index = index;
        }

        public string? Username => variables.Get(SpecialVariables.Git.Username(index));
        public string? Password => variables.Get(SpecialVariables.Git.Password(index));
        public string Url => variables.GetMandatoryVariable(SpecialVariables.Git.Url(index));
        public GitBranchName BranchName => new GitBranchName(variables.GetMandatoryVariable(SpecialVariables.Git.BranchName(index)));
        public string SubFolder => variables.GetMandatoryVariable(SpecialVariables.Git.SubFolder(index));
    }

    public interface IRepositoryConnection
    {
        public string? Username { get;  }
        public string? Password { get;  }
        public string Url { get;  }
    }
    
    public interface IGitConnection : IRepositoryConnection
    {
        public GitBranchName BranchName { get;  }
    }

    public interface IArgoApplicationSource : IGitConnection
    {
        public string SubFolder { get; }
    }
}