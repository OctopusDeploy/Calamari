using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Git;

/// <summary>
/// Discriminates an <see cref="IGitCredentialDto"/> on the <c>Kind</c> field emitted by Octopus
/// Server (matching the concrete type name). A missing <c>Kind</c> defaults to
/// <see cref="GitCredentialDto"/> for backwards compatibility with server versions that pre-date
/// the field.
/// </summary>
public class IGitCredentialDtoJsonConverter : JsonConverter<IGitCredentialDto>
{
    static readonly JsonSerializer ConcreteSerializer = new();

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, IGitCredentialDto? value, JsonSerializer serializer)
        => throw new NotSupportedException();

    public override IGitCredentialDto ReadJson(
        JsonReader reader,
        Type objectType,
        IGitCredentialDto? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var kind = obj["Kind"]?.Value<string>();

        return kind switch
        {
            null or nameof(GitCredentialDto) => obj.ToObject<GitCredentialDto>(ConcreteSerializer)!,
            nameof(SshKeyGitCredentialDto) => obj.ToObject<SshKeyGitCredentialDto>(ConcreteSerializer)!,
            _ => throw new JsonSerializationException($"Unrecognised credential Kind '{kind}'.")
        };
    }
}
