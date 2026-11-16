using Newtonsoft.Json;

namespace Calamari.Build.Utilities;

class AzureTokenResponse(string accessToken)
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = accessToken;
}