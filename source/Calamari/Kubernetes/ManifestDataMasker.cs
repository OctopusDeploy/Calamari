using System;
using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes
{
    public static class ManifestDataMasker
    {
        public static void MaskSensitiveData(YamlMappingNode rootNode)
        {
            using (var sha256 = SHA256.Create())
            {
                if (!rootNode.Children.TryGetValue("kind", out var kindNode) || !(kindNode is YamlScalarNode kindScalarNode))
                    return;
                
                //currently we only support secrets
                if (kindScalarNode.Value == "Secret")
                {
                    MaskSecretDataValues(rootNode, sha256);
                }
            }
        }

        static void MaskSecretDataValues(YamlMappingNode rootNode, SHA256 sha256)
        {
            if (!rootNode.Children.TryGetValue("data", out var dataNode) || !(dataNode is YamlMappingNode dataDictNode))
                return;

            foreach (var kvp in dataDictNode)
            {
                if (!(kvp.Value is YamlScalarNode valueScalarNode) || valueScalarNode.Value is null)
                    continue;

                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(valueScalarNode.Value));

                var redactedString = $"<redacted-{Convert.ToBase64String(hashedBytes)}>";

                valueScalarNode.Value = redactedString;
                //forcibly double quote to make it clear it's now a string
                valueScalarNode.Style = ScalarStyle.DoubleQuoted;
            }
        }
    }
}