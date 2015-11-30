using System.Collections.Generic;
using Newtonsoft.Json;

namespace Calamari.Azure.Deployment.Integration.ResourceGroups
{
    public interface IResourceGroupTemplateParameterParser
    {
        IDictionary<string, ResourceGroupTemplateParameter> ParseParameters(string json);
    }

    public class ResourceGroupTemplateParameterParser : IResourceGroupTemplateParameterParser
    {

        public IDictionary<string, ResourceGroupTemplateParameter> ParseParameters(string json)
        {
            Dictionary<string, ResourceGroupTemplateParameter> parameters;

            try
            {
                parameters = JsonConvert.DeserializeObject<Dictionary<string, ResourceGroupTemplateParameter>>(json);
            }
            catch (JsonSerializationException)
            {
                parameters =
                    new Dictionary<string, ResourceGroupTemplateParameter>(JsonConvert.DeserializeObject<ParameterEnvelope>(json).Parameters);
            }

            return parameters;
        }


        class ParameterEnvelope
        {
            [JsonProperty("$schema")]
            public string Schema { get; set; }

            [JsonProperty("contentVersion")]
            public string ContentVersion { get; set; }

            [JsonProperty("parameters")]
            public IDictionary<string, ResourceGroupTemplateParameter> Parameters { get; set; }
        }
    }
}