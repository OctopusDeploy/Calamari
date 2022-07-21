using System;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Substitutions
{
    public interface IFileSubstituter
    {
        void PerformSubstitution(string sourceFile, IVariables variables);
        void PerformSubstitution(string sourceFile, IVariables variables, string targetFile);
    }
}