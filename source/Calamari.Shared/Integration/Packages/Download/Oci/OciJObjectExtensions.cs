using System;
using Newtonsoft.Json.Linq;

namespace Calamari.Integration.Packages.Download.Oci
{
    static class OciJObjectExtensions
    {
        public static bool HasMediaTypeContaining(this JObject manifest, string value)
        {
            var mediaType = manifest[OciConstants.Manifest.MediaTypePropertyName];

            return mediaType != null
                   && mediaType.ToString().IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool HasConfigMediaTypeContaining(this JObject manifest, string value)
        {
            var config = manifest[OciConstants.Manifest.Config.PropertyName];

            return config is { Type: JTokenType.Object }
                   && config[OciConstants.Manifest.Config.MediaTypePropertyName] != null
                   && config[OciConstants.Manifest.Config.MediaTypePropertyName].ToString().IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool HasLayersMediaTypeContaining(this JObject manifest, string value)
        {
            var layers = manifest[OciConstants.Manifest.Layers.PropertyName];

            if (layers is { Type: JTokenType.Array })
            {
                foreach (var layer in layers)
                {
                    if (layer[OciConstants.Manifest.Layers.MediaTypePropertyName] != null
                        && layer[OciConstants.Manifest.Layers.MediaTypePropertyName].ToString().IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}