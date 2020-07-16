using System;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.StructuredVariables
{
    public interface IFileFormatVariableReplacer
    {
        // TODO: should be the same type as StructuredConfigVariablesModel.Format
        string SupportedFormat { get; }

        void ModifyFile(string filePath, IVariables variables);
    }
}