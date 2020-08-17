using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Calamari.Serialization
{
    public abstract class InheritedClassConverter<TBaseResource, TEnumType> : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            var contractResolver = serializer.ContractResolver;

            foreach (var property in value.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty)
                .Where(p => p.CanRead))
            {
                writer.WritePropertyName(getMappedPropertyName(contractResolver, property.Name));
                serializer.Serialize(writer, property.GetValue(value, null));
            }

            WriteTypeProperty(writer, value, serializer);

            writer.WriteEndObject();
        }

        protected virtual void WriteTypeProperty(JsonWriter writer, object value, JsonSerializer serializer)
        { }

        protected virtual Type? DefaultType { get; } = null;

        private static string getMappedPropertyName(IContractResolver resolver, string name)
        {
            return resolver is DefaultContractResolver result ? result.GetResolvedPropertyName(name) : name;
        }


        public override object? ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jo = JObject.Load(reader);
            var contractResolver = serializer.ContractResolver;
            var designatingProperty = jo.GetValue(getMappedPropertyName(contractResolver, TypeDesignatingPropertyName));


            Type type;
            if (designatingProperty == null)
            {
                if (DefaultType == null)
                {
                    throw new Exception($"Unable to determine type to deserialize. Missing property `{TypeDesignatingPropertyName}`");
                }
                type = DefaultType;
            }
            else
            {
                var derivedType = designatingProperty.ToObject<string>();
                var enumType = (TEnumType)Enum.Parse(typeof(TEnumType), derivedType);
                if (!DerivedTypeMappings.ContainsKey(enumType))
                {
                    throw new Exception($"Unable to determine type to deserialize. {TypeDesignatingPropertyName} `{enumType}` does not map to a known type");
                }

                type = DerivedTypeMappings[enumType];
            }

            var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
            var args = ctor.GetParameters().Select(p =>
                jo.GetValue(char.ToUpper(p.Name[0]) + p.Name.Substring(1))
                    .ToObject(p.ParameterType, serializer)).ToArray();
            var instance = ctor.Invoke(args);
            foreach (var prop in type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty)
                .Where(p => p.CanWrite))
            {
                var propertyName = getMappedPropertyName(contractResolver, prop.Name);
                var val = jo.GetValue(propertyName);
                if (val != null)
                {
                    prop.SetValue(instance, val.ToObject(prop.PropertyType, serializer), null);
                }
            }
            return instance;
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(TBaseResource).IsAssignableFrom(objectType);
        }

        protected abstract IDictionary<TEnumType, Type> DerivedTypeMappings { get; }

        protected abstract string TypeDesignatingPropertyName { get; }
    }
}
