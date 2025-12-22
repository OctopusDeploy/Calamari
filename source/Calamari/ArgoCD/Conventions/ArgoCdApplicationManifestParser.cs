using System;
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

            PopulateSourceType(application);
            
            return application;
        }

        static void PopulateSourceType(Application application)
        {
            for (int i = 0; i < application.Spec.Sources.Count; i++)
            {
                var source = application.Spec.Sources[i];
                var sourceType = i < application.Status.SourceTypes.Count ? application.Status.SourceTypes[i] : (SourceType?)null;
                
                source.SourceType = sourceType;
            }
        }
    }
}