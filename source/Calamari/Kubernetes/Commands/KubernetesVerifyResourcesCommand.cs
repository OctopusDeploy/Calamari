using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.Aws.Integration;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes.Conventions;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.Commands
{
    [Command(Name, Description = "Verifies that resources applied by a Kubernetes deploy step have reached their desired state")]
    public class KubernetesVerifyResourcesCommand : Command
    {
        public const string Name = "kubernetes-verify-resources";

        readonly ILog log;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly Kubectl kubectl;
        readonly IResourceStatusReportExecutor statusReporter;

        public KubernetesVerifyResourcesCommand(
            ILog log,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            Kubectl kubectl,
            IResourceStatusReportExecutor statusReporter)
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.kubectl = kubectl;
            this.statusReporter = statusReporter;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var json = variables.Get(SpecialVariables.AppliedResources);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new CommandException($"The applied resources variable was not found. This variable is required to verify the deployed resources.");
            }

            List<ResourceIdentifier> resources;
            try
            {
                resources = DeserializeResources(json);
            }
            catch (JsonException ex)
            {
                throw new CommandException($"Could not parse applied resources output variable: {ex.Message}");
            }

            if (resources.Count == 0)
            {
                log.Info("Applied resources list is empty; nothing to verify.");
                return 0;
            }

            WarnForUnverifiableResources(resources);

            AuthenticateKubectl();

            var timeoutSeconds = variables.GetInt32(SpecialVariables.Timeout) ?? 0;
            var waitForJobs = variables.GetFlag(SpecialVariables.WaitForJobs);

            var statusCheck = statusReporter.Start(timeoutSeconds, waitForJobs, resources);
            var success = statusCheck.WaitForCompletionOrTimeout(CancellationToken.None).GetAwaiter().GetResult();

            if (!success)
            {
                throw new CommandException("Resource verification failed. Check verbose logs for more details.");
            }

            return 0;
        }

        static List<ResourceIdentifier> DeserializeResources(string json)
        {
            var raw = JsonConvert.DeserializeObject<List<AppliedResourceDto>>(json) ?? new List<AppliedResourceDto>();
            return raw
                   .Select(r => new ResourceIdentifier(
                                                       new ResourceGroupVersionKind(r.Group ?? string.Empty, r.Version, r.Kind),
                                                       r.Name,
                                                       r.Namespace))
                   .ToList();
        }

        void WarnForUnverifiableResources(IEnumerable<ResourceIdentifier> resources)
        {
            foreach (var resource in resources)
            {
                var gvk = resource.GroupVersionKind;
                if (ResourceFactory.IsVerifiable(gvk))
                    continue;

                var name = string.IsNullOrEmpty(resource.Namespace) ? resource.Name : $"{resource.Namespace}/{resource.Name}";
                log.Warn($"Unable to fully verify resource '{gvk}' '{name}'. Calamari does not know the readiness criteria for this resource type; only its existence will be confirmed.");
            }
        }

        void AuthenticateKubectl()
        {
            var deployment = new RunningDeployment(variables);
            kubectl.SetWorkingDirectory(deployment.CurrentDirectory);
            kubectl.SetEnvironmentVariables(deployment.EnvironmentVariables);

            var conventions = new List<IConvention>();
            if (variables.Get(Deployment.SpecialVariables.Account.AccountType) == "AmazonWebServicesAccount")
            {
                conventions.Add(new AwsAuthConvention(log, variables));
            }
            conventions.Add(new KubernetesAuthContextConvention(log, commandLineRunner, kubectl, fileSystem));

            new ConventionProcessor(deployment, conventions, log).RunConventions();
        }

        class AppliedResourceDto
        {
            public string Group { get; set; }
            public string Version { get; set; }
            public string Kind { get; set; }
            public string Name { get; set; }
            public string Namespace { get; set; }
        }
    }
}
