using System.Collections.Generic;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes.Conventions
{
    public class HelmResourceStatusConvention : IInstallConvention
    {
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly IResourceStatusReportExecutor statusReporter;

        public HelmResourceStatusConvention(ILog log, ICommandLineRunner commandLineRunner, IResourceStatusReportExecutor statusReporter)
        {
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.statusReporter = statusReporter;
        }

        public void Install(RunningDeployment deployment)
        {
            if (!deployment.Variables.GetFlag(SpecialVariables.ResourceStatusCheck))
            {
                return;
            }

            var timeoutSeconds = deployment.Variables.GetInt32(SpecialVariables.Timeout) ?? 0;
            var waitForJobs = deployment.Variables.GetFlag(SpecialVariables.WaitForJobs);

            var statusCheck = statusReporter.Start(timeoutSeconds, waitForJobs);

            var releaseName = deployment.Variables.Get("ReleaseName");

            var helm = new HelmCli(log, commandLineRunner, deployment.CurrentDirectory, deployment.EnvironmentVariables)
                       .WithExecutable(deployment.Variables)
                       .WithNamespace(deployment.Variables.Get(SpecialVariables.Helm.Namespace));

            var manifest = helm.GetManifest(releaseName);

            var resources = new List<ResourceIdentifier>();
            using (var reader = new StringReader(manifest))
            {
                var stream = new YamlStream();
                stream.Load(reader);

                foreach (var yamlDocument in stream.Documents)
                {
                    if (!(yamlDocument.RootNode is YamlMappingNode rootNode))
                    {
                        log.Warn("Could not parse manifest, resources will not be added to live object status");
                        continue;
                    }

                    if (!rootNode.Children.TryGetValue("metadata", out var node) || !(node is YamlMappingNode metadataNode))
                    {
                        log.Warn("Could not parse manifest, resources will not be tracked by resource status");
                        continue;
                    }

                    resources.Add(new ResourceIdentifier(
                                                         GetScalarValue(rootNode, "kind"),
                                                         GetScalarValue(metadataNode, "name"),
                                                         GetScalarValue(metadataNode, "namespace")
                                                        ));
                }

                statusCheck.AddResources(resources.ToArray());
            }

            statusCheck.WaitForCompletionOrTimeout().GetAwaiter().GetResult();
        }

        static string GetScalarValue(YamlMappingNode mappingNode, string key)
        {
            return mappingNode.Children.TryGetValue(key, out var node) && node is YamlScalarNode scalarNode
                ? scalarNode.Value
                : null;
        }
    }
}