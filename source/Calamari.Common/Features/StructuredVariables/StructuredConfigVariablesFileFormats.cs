using System;

namespace Calamari.Common.Features.StructuredVariables
{
    public static class StructuredConfigVariablesFileFormats
    {
        // TODO: ask if we should use static readonly instead, or a tiny type, or an enum.
        public const string Json = "Json";
        
        public const string Yaml = "Yaml";

        // TODO: this is temporary, until we have autofac everywhere.
        public static readonly IFileFormatVariableReplacer[] AllReplacers = {
            new JsonFormatVariableReplacer(),
            new YamlFormatVariableReplacer()
        };
    }
}