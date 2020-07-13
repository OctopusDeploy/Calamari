using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.StructuredVariables
{
    public interface IFileFormatVariableReplacer
    {
        string FileFormatName { get; }
        
        bool TryModifyFile(string filePath, IVariables variables);
    }
}