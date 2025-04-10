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
        readonly IKubernetesManifestNamespaceResolver namespaceResolver;

        static readonly ISerializer YamlSerializer = new SerializerBuilder()
            .Build();

        public ManifestReporter(IVariables variables, ICalamariFileSystem fileSystem, ILog log, IKubernetesManifestNamespaceResolver namespaceResolver)
        {
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.log = log;
            this.namespaceResolver = namespaceResolver;
        }

        public void ReportManifestFileApplied(string filePath)
        {
            if (!FeatureToggle.KubernetesLiveObjectStatusFeatureToggle.IsEnabled(variables)
                && !OctopusFeatureToggles.KubernetesObjectManifestInspectionFeatureToggle.IsEnabled(variables))
                return;

            try
            {
                using (var yamlFile = fileSystem.OpenFile(filePath, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(yamlFile))
                {
                    //read the manifest
                    var manifest = reader.ReadToEnd();
                    ReportManifestApplied(manifest);
                }
            }
            catch (Exception e)
            {
                log.Warn($"Failed to read yaml manifest at {filePath}, resources will not be added to live object status.");
                log.Verbose($"Error: {e.Message}");
            }
        }

        public void ReportManifestApplied(string yamlManifest)
        {
            if (!FeatureToggle.KubernetesLiveObjectStatusFeatureToggle.IsEnabled(variables) && !OctopusFeatureToggles.KubernetesObjectManifestInspectionFeatureToggle.IsEnabled(variables))
                return;

            try
            {
                var yamlStream = new YamlStream();
                yamlStream.Load(new StringReader(yamlManifest));
                ReportManifestStreamApplied(yamlStream);
            }
            catch (YamlException e)
            {
                LogYamlException(e, yamlManifest);
            }
        }

        void ReportManifestStreamApplied(YamlStream yamlStream)
        {
            foreach (var document in yamlStream.Documents)
            {
                if (!(document.RootNode is YamlMappingNode rootNode))
                {
                    log.Warn("Could not parse manifest, resources will not be added to live object status.");
                    continue;
                }

                var updatedDocument = SerializeManifest(rootNode);

                var ns = namespaceResolver.ResolveNamespace(rootNode, variables);

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

        void LogYamlException(YamlException e, string manifest)
        {
            if (variables.GetFlag(SpecialVariables.PrintVerboseManifestOnParsingError))
            {
                log.Warn("Invalid YAML syntax found, resources will not be added to live object status. The error and manifest are verbose logged below.");
                log.Verbose("---------------------------");
                log.Verbose($"Error: {e.Message}");
                log.Verbose("---------------------------");
                log.Verbose(manifest);
                log.Verbose("---------------------------");
            }
            else
            {
                log.Warn($"Invalid YAML syntax found, resources will not be added to live object status. To view the error and manifest, set Octopus Variable '{SpecialVariables.PrintVerboseManifestOnParsingError}' to 'true'");
            }
        }

        static string SerializeManifest(YamlMappingNode node)
        {
            return YamlSerializer.Serialize(node);
        }
    }
}