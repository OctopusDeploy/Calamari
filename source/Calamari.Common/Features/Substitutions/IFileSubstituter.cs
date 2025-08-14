using System;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Substitutions
{
    public interface IFileSubstituter
    {
        void PerformSubstitution(string sourceFile);
        void PerformSubstitution(string sourceFile, string targetFile);
    }
}