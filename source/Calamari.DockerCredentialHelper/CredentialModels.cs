using System.Text.Json.Serialization;

namespace Calamari.DockerCredentialHelper
{
    public class DockerCredential
    {
        public string Username { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
    }

    public class StoreRequest
    {
        public string ServerURL { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
    }

    public class GetResponse
    {
        public string ServerURL { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
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
