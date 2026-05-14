using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.Calamari.Contracts.Git;

namespace Calamari.CommitToGit;

/// <summary>
/// Discriminates an <see cref="IGitCredentialDto"/> on the <c>Type</c> field emitted by Octopus
/// Server. A missing <c>Type</c> defaults to <see cref="GitUsernameAndPasswordCredentialDto"/> for
/// backwards compatibility with server versions that pre-date the field.
/// </summary>
public class GitCredentialDtoJsonConverter : JsonConverter
{
    static readonly JsonSerializer ConcreteSerializer = new();

    public override bool CanConvert(Type objectType) => objectType == typeof(IGitCredentialDto);

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        => throw new NotSupportedException();

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var type = obj["Type"]?.Value<string>();

        return type switch
        {
            null or GitUsernameAndPasswordCredentialDto.DiscriminatorValue
                => obj.ToObject<GitUsernameAndPasswordCredentialDto>(ConcreteSerializer),
            GitSshKeyAndKnownHostsDto.DiscriminatorValue
                => obj.ToObject<GitSshKeyAndKnownHostsDto>(ConcreteSerializer),
            _ => throw new JsonSerializationException($"Unrecognised credential Type '{type}'.")
        };
    }
}
