using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.Common.Features.StructuredVariables
{
    public static class FileFormatVariableReplacers
    {
        // TODO: Once we have a good DI solution this can be removed.
        public static IFileFormatVariableReplacer[] BuildAllReplacers(ICalamariFileSystem fileSystem)
        {
            return new IFileFormatVariableReplacer[]
            {
                new JsonFormatVariableReplacer(fileSystem),
                new YamlFormatVariableReplacer()
            };
        }
    }
}