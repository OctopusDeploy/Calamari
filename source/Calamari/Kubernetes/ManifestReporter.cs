using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Calamari.Kubernetes
{
    public interface IManifestReporter
    {
        void ReportManifestApplied(string filePath, string implicitNamespace);
    }

    public class ManifestReporter : IManifestReporter
    {
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        static readonly ISerializer YamlSerializer = new SerializerBuilder()
            .Build();

        public ManifestReporter(IVariables variables, ICalamariFileSystem fileSystem, ILog log)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public void ReportManifestApplied(string filePath, string implicitNamespace)
        {
            using (var yamlFile = fileSystem.OpenFile(filePath, FileAccess.Read, FileShare.Read))
            {
                try
                {
                    var yamlStream = new YamlStream();
                    yamlStream.Load(new StreamReader(yamlFile));

                    foreach (var document in yamlStream.Documents)
                    {
                        if (!(document.RootNode is YamlMappingNode rootNode))
                        {
                            log.Warn("Could not parse manifest, resources will not be added to live object status");
                            continue;
                        }

                        var updatedDocument = SerializeManifest(rootNode);

                        var dict = new Dictionary<string, string>
                        {
                            [SpecialVariables.ServiceMessageNames.ManifestApplied.ManifestAttribute] = updatedDocument,
                        };
                        
                        var ns = GetNamespaceFromManifest(rootNode) ?? implicitNamespace;
                        if (!string.IsNullOrWhiteSpace(ns))
                        {
                            dict[SpecialVariables.ServiceMessageNames.ManifestApplied.NamespaceAttribute] = ns;
                        }

                        log.WriteServiceMessage(new ServiceMessage(SpecialVariables.ServiceMessageNames.ManifestApplied.Name, dict));
                    }
                }
                catch (SemanticErrorException)
                {
                    log.Warn("Invalid YAML syntax found, resources will not be added to live object status");
                }
            }
        }

        static string GetNamespaceFromManifest(YamlMappingNode yamlRoot)
        {
            return yamlRoot.Children.TryGetValue("metadata", out var metadataNode) && metadataNode is YamlMappingNode metadataMappingNode && metadataMappingNode.Children.TryGetValue("namespace", out var namespaceNode) && namespaceNode is YamlScalarNode namespaceScalarNode && !string.IsNullOrWhiteSpace(namespaceScalarNode.Value)
                ? namespaceScalarNode.Value
                : null;
        }

        static string SerializeManifest(YamlMappingNode node)
        {
            //mask any sensitive data in the manifest
            ManifestDataMasker.MaskSensitiveData(node);

            return YamlSerializer.Serialize(node);
        }
    }
}