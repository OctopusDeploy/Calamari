using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.StructuredVariables
{
    public static class FileFormatVariableReplacers
    {
        public static IFileFormatVariableReplacer[] BuildAllReplacers(ICalamariFileSystem fileSystem, ILog log)
        {
            return new IFileFormatVariableReplacer[]
            {
                new JsonFormatVariableReplacer(fileSystem, log),
                new YamlFormatVariableReplacer(log),
                new XmlFormatVariableReplacer(fileSystem, log),
                new PropertiesFormatVariableReplacer(fileSystem)
            };
        }
    }
}