namespace Calamari.Common.Features.StructuredVariables
{
    public interface IYamlFormatVariableReplacer : IFileFormatVariableReplacer
    {
    }

    public class YamlFormatVariableReplacer : IYamlFormatVariableReplacer
    {
        public string FileFormatName => "YAML";
        
        public bool TryModifyFile(string filePath, IVariables variables)
        {
            // Yaml not yet supported.
            return false;
        }
    }
}