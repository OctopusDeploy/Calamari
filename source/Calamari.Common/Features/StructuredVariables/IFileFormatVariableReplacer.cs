namespace Calamari.Features.StructuredVariables
{
    public interface IFileFormatVariableReplacer
    {
        string FileFormatName { get; }
        
        bool TryModifyFile(string filePath, IVariables variables);
    }
}