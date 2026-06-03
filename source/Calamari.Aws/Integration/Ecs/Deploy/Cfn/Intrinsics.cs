using System;
using Newtonsoft.Json;

namespace Calamari.Aws.Integration.Ecs.Deploy.Cfn;

// The CloudFormation { "Ref": "Name" } intrinsic. Used for parameter references,
// resource logical-ID references, and pseudo-parameters like "AWS::Region".
public sealed record Ref
{
    [JsonProperty("Ref")]
    public string Reference { get; }

    public Ref(string reference) => Reference = reference;
}

// Wrapper for slots where a CloudFormation property can be either a literal value
// or a Ref intrinsic. Implicit conversions keep call sites tight:
//   props.Cpu = "256";                              // literal
//   props.Cpu = new Ref("TaskDefinitionCPU");       // Ref
// At serialisation, ValueConverter writes just the literal or just the Ref —
// the wrapper itself never appears in the JSON.
[JsonConverter(typeof(ValueConverter))]
public sealed record Value<T>
{
    public T Literal { get; init; }
    public Ref Reference { get; init; }

    public static implicit operator Value<T>(T literal) => new() { Literal = literal };
    public static implicit operator Value<T>(Ref reference) => new() { Reference = reference };
}

 sealed class ValueConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) =>
        objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Value<>);

    public override bool CanRead => false;

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        var type = value.GetType();
        var reference = type.GetProperty(nameof(Value<object>.Reference))!.GetValue(value);
        var literal = type.GetProperty(nameof(Value<object>.Literal))!.GetValue(value);

        serializer.Serialize(writer, reference ?? literal);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) =>
        throw new NotSupportedException("Value<T> is write-only — we don't parse CFN templates back into the model.");
}
