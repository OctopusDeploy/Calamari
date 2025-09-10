using System.Text.Json;
using System.Text.Json.Nodes;
using Calamari.ArgoCD.Domain;

namespace Calamari.ArgoCD.Conventions
{
    public interface IArgoCDApplicationManifestParser
    {
        Application ParseManifest(string applicationManifest);
    }

    public class ArgoCdApplicationManifestParser : IArgoCDApplicationManifestParser
    {
        public Application ParseManifest(string applicationManifest)
        {
            var node = JsonNode.Parse(applicationManifest);
            var application = node.Deserialize<Application>(JsonSerializerOptions.Default);

            return application;
        }
    }
}