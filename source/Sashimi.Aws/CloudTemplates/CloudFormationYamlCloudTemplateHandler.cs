using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Octopus.Server.Extensibility.Metadata;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.CloudTemplates;
using YamlDotNet.RepresentationModel;

namespace Sashimi.Aws.CloudTemplates
{
    class CloudFormationYamlCloudTemplateHandler : ICloudTemplateHandler
    {

        readonly IFormatIdentifier formatIdentifier;

        public CloudFormationYamlCloudTemplateHandler(IFormatIdentifier formatIdentifier)
        {
            this.formatIdentifier = formatIdentifier;
        }

        public bool CanHandleTemplate(string providerId, string template)
            => AwsConstants.CloudTemplateProviderIds.Contains(providerId, StringComparer.OrdinalIgnoreCase) &&
                formatIdentifier.IsYaml(template);

        public Metadata ParseTypes(string template)
        {
            var metadata = new Metadata();
            var parameters = GetParameters(template);

            metadata.Types = new List<TypeMetadata>
            {
                new TypeMetadata
                {
                    Name = AwsDataTypes.CloudFormationTemplateTypeName,
                    Properties = parameters != null
                        ? parameters.Select(p =>
                            new PropertyMetadata
                            {
                                DisplayInfo = new DisplayInfo
                                {
                                    Description = GetYamlValue(p.Value, "Description"),
                                    Label = GetYamlValue(p.Key),
                                    Required = true,
                                    Options = GetOptions(p.Value, "AllowedValues")
                                },
                                Name = GetYamlValue(p.Key),
                                Type = GetType(p.Value)
                            }).ToList()
                        : new List<PropertyMetadata>()
                }
            };

            return metadata;
        }

        public object ParseModel(string template)
        {
            var parameters = GetParameters(template);
            return parameters?.Select(x => new KeyValuePair<string, object>(x.Key.ToString(), GetDefaultValue(x.Value))).ToDictionary(x => x.Key, x => x.Value) ?? new Dictionary<string, object>();
        }

        object GetDefaultValue(YamlNode node)
        {
            var defaultValue = GetYamlValue(node, "Default");
            return defaultValue;
        }

        string GetType(YamlNode node)
        {
            return AwsDataTypes.MapToType(GetYamlValue(node, "Type"));
        }

        YamlMappingNode GetParameters(string template)
        {
            StringReader sr = new StringReader(template);
            var yaml = new YamlStream();
            yaml.Load(sr);
            var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
            var parameters = mapping.Children.ContainsKey("Parameters") ? mapping.Children[new YamlScalarNode("Parameters")] as YamlMappingNode : null;
            return parameters;
        }

        Dictionary<string, YamlScalarNode> scalarNodeCache = new Dictionary<string, YamlScalarNode>();

        YamlScalarNode GetYamlScalarNodeInstance(string nodeName)
        {
            YamlScalarNode scalarNode = null;
            if (scalarNodeCache.ContainsKey(nodeName))
            {
                scalarNode = scalarNodeCache[nodeName];
            }
            else
            {
                scalarNode = new YamlScalarNode(nodeName);
                scalarNodeCache[nodeName] = scalarNode;
            }
            return scalarNode;
        }

        string GetYamlValue(YamlNode node, string nodeName)
        {
            YamlScalarNode scalarNode = GetYamlScalarNodeInstance(nodeName);
            var mappingNode = node as YamlMappingNode;
            if (!mappingNode.Children.ContainsKey(nodeName))
            {
                return null;
            }

            return (mappingNode[scalarNode] as YamlScalarNode).Value;
        }

        string GetYamlValue(YamlNode node)
        {
            return (node as YamlScalarNode).Value;

        }

        OptionsMetadata GetOptions(YamlNode node, string nodeName)
        {
            if (node is YamlMappingNode mappingNode && mappingNode.Children.ContainsKey(nodeName))
            {
                if (mappingNode[nodeName] is YamlSequenceNode optionsNode)
                {
                    OptionsMetadata options = new OptionsMetadata();
                    options.SelectMode = "Single";
                    options.Values = optionsNode.ToDictionary(GetYamlValue, GetYamlValue);
                    return options;
                }
            }

            return null;
        }


    }
}