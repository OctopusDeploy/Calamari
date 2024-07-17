using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Deployment.Conventions.DependencyVariablesStrategies
{
    public class PackageVariablesFactory : IDependencyVariablesFactory
    {
        public IDependencyVariables GetDependencyVariables(IVariables variables) => new PackageDependencyVariables(variables);

        class PackageDependencyVariables : IDependencyVariables
        {
            readonly IVariables variables;

            public PackageDependencyVariables(IVariables variables)
            {
                this.variables = variables;
            }

            public IEnumerable<string> GetIndexes() => variables.GetIndexes(PackageVariables.PackageCollection).WhereNotNullOrWhiteSpace();
            public string OriginalPath(string referenceName) => variables.Get(PackageVariables.IndexedOriginalPath(referenceName));
            public bool Extract(string referenceName) => variables.GetFlag(PackageVariables.IndexedExtract(referenceName));

            public IDependencyOutputVariables OutputVariables { get; } = new PackageDependencyOutputVariables();

            class PackageDependencyOutputVariables : IDependencyOutputVariables
            {
                public string ExtractedPath(string referenceName) => SpecialVariables.Packages.ExtractedPath(referenceName);
                public string FilePath(string referenceName) => SpecialVariables.Packages.PackageFilePath(referenceName);
                public string FileName(string referenceName) => SpecialVariables.Packages.PackageFileName(referenceName);
            }
        }
    }
}