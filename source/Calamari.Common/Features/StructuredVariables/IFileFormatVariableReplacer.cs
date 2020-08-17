using System;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.StructuredVariables
{
    public interface IFileFormatVariableReplacer
    {
        string FileFormatName { get; }

        bool IsBestReplacerForFileName(string fileName);

        void ModifyFile(string filePath, IVariables variables);
    }
}