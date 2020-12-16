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
                // For files that don't have well known extensions (like `.xml`, `.yaml`, etc.)
                // these replacers should appear here in the order we want to test them against such files
                new JsonFormatVariableReplacer(fileSystem, log),
                new XmlFormatVariableReplacer(fileSystem, log),
                new YamlFormatVariableReplacer(fileSystem, log),
                new PropertiesFormatVariableReplacer(fileSystem, log)
            };
        }
    }
}