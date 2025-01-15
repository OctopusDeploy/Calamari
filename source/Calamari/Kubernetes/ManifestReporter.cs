using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Calamari.Kubernetes
{
    public interface IManifestReporter
    {
        void ReportManifestFileApplied(string filePath);
        void ReportManifestApplied(string yaml);
    }

    public class ManifestReporter : IManifestReporter
    {
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly IApiResourceScopeLookup resourceScopeLookup;

        static readonly ISerializer YamlSerializer = new SerializerBuilder()
            .Build();

        public ManifestReporter(IVariables variables, ICalamariFileSystem fileSystem, ILog log, IApiResourceScopeLookup resourceScopeLookup)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.log = log;
            this.resourceScopeLookup = resourceScopeLookup;
        }

        string GetNamespace(YamlMappingNode yamlRoot)
        {
            //we check to see if there is an explicit helm namespace defined first
            //then fallback on the action/target default namespace
            //otherwise fallback on default
            var implicitNamespace = variables.Get(SpecialVariables.Helm.Namespace) ?? variables.Get(SpecialVariables.Namespace) ?? "default";

            if (yamlRoot.Children.TryGetValue("metadata", out var metadataNode) && metadataNode is YamlMappingNode metadataMappingNode && metadataMappingNode.Children.TryGetValue("namespace", out var namespaceNode) && namespaceNode is YamlScalarNode namespaceScalarNode && !string.IsNullOrWhiteSpace(namespaceScalarNode.Value))
            {
                implicitNamespace = namespaceScalarNode.Value;
            }

            return implicitNamespace;
        }

        public void ReportManifestFileApplied(string filePath)
        {
            if (!FeatureToggle.KubernetesLiveObjectStatusFeatureToggle.IsEnabled(variables)
                && !OctopusFeatureToggles.KubernetesObjectManifestInspectionFeatureToggle.IsEnabled(variables))
                return;

            using (var yamlFile = fileSystem.OpenFile(filePath, FileAccess.Read, FileShare.Read))
            {
                try
                {
                    var yamlStream = new YamlStream();
                    yamlStream.Load(new StreamReader(yamlFile));
                    ReportManifestStreamApplied(yamlStream);
                }
                catch (SemanticErrorException)
                {
                    log.Warn("Invalid YAML syntax found, resources will not be added to live object status");
                }
            }
        }

        public void ReportManifestApplied(string yamlManifest)
        {
            if (!FeatureToggle.KubernetesLiveObjectStatusFeatureToggle.IsEnabled(variables)
                && !OctopusFeatureToggles.KubernetesObjectManifestInspectionFeatureToggle.IsEnabled(variables))
                return;

            try
            {
                var yamlStream = new YamlStream();
                yamlStream.Load(new StringReader(yamlManifest));
                ReportManifestStreamApplied(yamlStream);
            }
            catch (SemanticErrorException)
            {
                log.Warn("Invalid YAML syntax found, resources will not be added to live object status");
            }
        }

        void ReportManifestStreamApplied(YamlStream yamlStream)
        {
            foreach (var document in yamlStream.Documents)
            {
                if (!(document.RootNode is YamlMappingNode rootNode))
                {
                    log.Warn("Could not parse manifest, resources will not be added to live object status");
                    continue;
                }

                var updatedDocument = SerializeManifest(rootNode);

                var apiResourceIdentifier = GetApiResourceIdentifier(rootNode);

                var ns = GetNamespace(rootNode);

                if (resourceScopeLookup.TryGetIsNamespaceScoped(apiResourceIdentifier, out var isNamespaceScoped))
                {
                    //if the resource is cluster scoped, remove the namespace
                    if (!isNamespaceScoped)
                    {
                        ns = null;
                    }
                }
                else
                {
                    //if we can't determine the resource scope, log a verbose message
                    log.Verbose($"Unable to determine if resource type {apiResourceIdentifier} is namespaced. Using namespace value on the manifest.");
                }

                var message = new ServiceMessage(
                                                 SpecialVariables.ServiceMessages.ManifestApplied.Name,
                                                 new Dictionary<string, string>
                                                 {
                                                     { SpecialVariables.ServiceMessages.ManifestApplied.ManifestAttribute, updatedDocument },
                                                     { SpecialVariables.ServiceMessages.ManifestApplied.NamespaceAttribute, ns }
                                                 });

                log.WriteServiceMessage(message);
            }
        }

        static ApiResourceIdentifier GetApiResourceIdentifier(YamlMappingNode node)
        {
            var apiVersion = node.Children.TryGetValue("apiVersion", out var apiVersionNode) && apiVersionNode is YamlScalarNode apiVersionScalarNode ? apiVersionScalarNode.Value : null;
            var kind = node.Children.TryGetValue("kind", out var kindNode) && kindNode is YamlScalarNode kindScalarNode ? kindScalarNode.Value : null;
            return new ApiResourceIdentifier(apiVersion, kind);
        }

        static string SerializeManifest(YamlMappingNode node)
        {
            return YamlSerializer.Serialize(node);
        }
    }
}