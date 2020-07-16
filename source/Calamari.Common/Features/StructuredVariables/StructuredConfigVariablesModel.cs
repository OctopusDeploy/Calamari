using Newtonsoft.Json;

namespace Calamari.Common.Features.StructuredVariables
{
    public class StructuredConfigVariablesModel
    {
        [JsonConstructor]
        public StructuredConfigVariablesModel(string format, string target)
        {
            Format = format;
            Target = target;
        }
        
        // TODO: should match the type of IFileFormatVariableReplacer.Format
        public string Format { get; }
        
        public string Target { get; }
    }
}