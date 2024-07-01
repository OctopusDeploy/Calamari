using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Deployment.Conventions.DependencyVariablesStrategies
{
    public class GitDependencyVariablesStrategy : IDependencyVariablesStrategy
    {
        public IDependencyVariables GetDependencyVariables(IVariables variables) => new GitDependencyVariables(variables);

        class GitDependencyVariables : IDependencyVariables
        {
            readonly IVariables variables;

            public GitDependencyVariables(IVariables variables)
            {
                this.variables = variables;
                OutputVariables = new GitDependencyOutputVariables();
            }

            public IEnumerable<string> GetIndexes() => variables.GetIndexes(SpecialVariables.GitResources.GitResourceCollection).WhereNotNullOrWhiteSpace();
            public string OriginalPath(string referenceName) => variables.Get(SpecialVariables.GitResources.OriginalPath(referenceName));
            public bool Extract(string referenceName) => variables.GetFlag(SpecialVariables.GitResources.Extract(referenceName));

            public IDependencyOutputVariables OutputVariables { get; }

            class GitDependencyOutputVariables : IDependencyOutputVariables
            {
                public string ExtractedPath(string referenceName) => SpecialVariables.GitResources.ExtractedPath(referenceName);
                public string FilePath(string referenceName) => SpecialVariables.GitResources.FilePath(referenceName);
                public string FileName(string referenceName) => SpecialVariables.GitResources.FileName(referenceName);
            }
        }
    }
}