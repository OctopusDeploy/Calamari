using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Azure.Deployment.Integration.ResourceGroups
{
    public interface IResourceGroupTemplateNormalizer
    {
        string Normalize(string json);
    }

    /// <summary>
    /// There are 2 ways the parameters can be passed to Clamari but the Resource Group Client supports only one format so we need to
    /// normalize the input. Have a look at ResourceGroupTemplateNormalizerFixture for more details.
    /// </summary>
    public class ResourceGroupTemplateNormalizer : IResourceGroupTemplateNormalizer
    {
        public string Normalize(string json)
        {
            try
            {
                var envelope = JsonConvert.DeserializeObject<ParameterEnvelope>(json);
                return JsonConvert.SerializeObject(envelope.Parameters);
            }
            catch (JsonSerializationException)
            {
                return json;
            }
        }

        class ParameterEnvelope
        {
            [JsonProperty("parameters", Required = Required.Always)]
            public JObject Parameters { get; set; }
        }
    }
}