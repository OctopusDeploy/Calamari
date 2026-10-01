using System.Text.Json.Serialization;

namespace Calamari.DockerCredentialHelper
{
    public record DockerCredential
    {
        public string Username { get; init; } = string.Empty;
        public string Secret { get; init; } = string.Empty;
    }

    public record StoreRequest
    {
        public string ServerURL { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public string Secret { get; init; } = string.Empty;
    }

    public record GetResponse
    {
        public string ServerURL { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public string Secret { get; init; } = string.Empty;
    }

    // Source-generated serialization keeps System.Text.Json trim-safe (no reflection), so the
    // published binary can be trimmed without losing (de)serialization of these types.
    [JsonSerializable(typeof(DockerCredential))]
    [JsonSerializable(typeof(StoreRequest))]
    [JsonSerializable(typeof(GetResponse))]
    internal partial class CredentialJsonContext : JsonSerializerContext
    {
    }
}
