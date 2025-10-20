using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain.Converters
{
    public abstract class SourcesConverter : JsonConverter<List<SourceBase>>
    {
        public override List<SourceBase> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // This will be handled at the property level, but we need this for the attribute
            throw new NotImplementedException("Use SingleOrArrayConverter instead");
        }

        public override void Write(Utf8JsonWriter writer, List<SourceBase> value, JsonSerializerOptions options)
        {
            // This will be handled at the property level
            throw new NotImplementedException("Use SingleOrArrayConverter instead");
        }
    }
}
