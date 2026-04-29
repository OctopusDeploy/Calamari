using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Amazon.ECS;
using Amazon.ECS.Model;
using Calamari.Aws.Integration.Ecs.Update;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Task = System.Threading.Tasks.Task;

namespace Calamari.Aws.Deployment.Conventions;

public class UpdateEcsServiceConvention : IInstallConvention
{
    readonly IAmazonECS ecs;
    readonly EcsUpdateServiceInputs inputs;
    readonly IReadOnlyList<KeyValuePair<string, string>> tags;
    readonly ILog log;
    readonly Func<TimeSpan> deploymentPollInterval;
    readonly Func<TimeSpan> taskPollInterval;

    public const string OutputRevisionVar = "Octopus.Action.Aws.Ecs.Output.TaskDefinitionRevision";
    public const string OutputFamilyVar = "Octopus.Action.Aws.Ecs.Output.TaskDefinitionFamily";

    public UpdateEcsServiceConvention(
        IAmazonECS ecs,
        EcsUpdateServiceInputs inputs,
        IReadOnlyList<KeyValuePair<string, string>> tags,
        ILog log,
        Func<TimeSpan> deploymentPollInterval = null,
        Func<TimeSpan> taskPollInterval = null)
    {
        this.ecs = ecs;
        this.inputs = inputs;
        this.tags = tags;
        this.log = log;
        this.deploymentPollInterval = deploymentPollInterval ?? (() => TimeSpan.FromSeconds(3));
        this.taskPollInterval = taskPollInterval ?? (() => TimeSpan.FromSeconds(10));
    }

    public void Install(RunningDeployment deployment) => InstallAsync(deployment).GetAwaiter().GetResult();

    public async Task InstallAsync(RunningDeployment deployment)
    {
        var ct = CancellationToken.None;

        var template = (await ecs.DescribeTaskDefinitionAsync(
            new DescribeTaskDefinitionRequest { TaskDefinition = inputs.TemplateTaskDefinitionName, Include = ["TAGS"] }, ct)).TaskDefinition
            ?? throw new CommandException($"Template task definition '{inputs.TemplateTaskDefinitionName}' not found.");

        var mutated = TaskDefinitionMutator.Apply(template, inputs.Containers);
        mutated.Family = inputs.TargetTaskDefinitionName;

        var existingTarget = (await ecs.DescribeTaskDefinitionAsync(
            new DescribeTaskDefinitionRequest { TaskDefinition = inputs.TargetTaskDefinitionName, Include = ["TAGS"] }, ct)).TaskDefinition
            ?? throw new CommandException($"Existing destination task definition '{inputs.TargetTaskDefinitionName}' not found.");

        TaskDefinition newTaskDefinition = null;
        if (!TaskDefinitionEquality.AreSame(mutated, existingTarget))
        {
            var register = BuildRegisterRequest(mutated, tags);
            var registerResp = await ecs.RegisterTaskDefinitionAsync(register, ct);
            newTaskDefinition = registerResp.TaskDefinition;
        }

        var serviceResp = await ecs.DescribeServicesAsync(new DescribeServicesRequest
        {
            Cluster = inputs.ClusterName,
            Services = [inputs.ServiceName],
            Include = ["TAGS"]
        }, ct);
        var existingService = serviceResp.Services?.FirstOrDefault()
            ?? throw new CommandException($"Service '{inputs.ServiceName}' not found in cluster '{inputs.ClusterName}'.");

        var newRevisionRef = newTaskDefinition is null
            ? existingService.TaskDefinition
            : $"{newTaskDefinition.Family}:{newTaskDefinition.Revision}";

        Service updatedService = existingService;
        if (existingService.TaskDefinition != newRevisionRef)
        {
            var updateResp = await ecs.UpdateServiceAsync(new UpdateServiceRequest
            {
                Cluster = inputs.ClusterName,
                Service = inputs.ServiceName,
                TaskDefinition = newRevisionRef,
                ForceNewDeployment = true
            }, ct);
            updatedService = updateResp.Service;

            if (tags.Count > 0)
            {
                await ecs.TagResourceAsync(new TagResourceRequest
                {
                    ResourceArn = updatedService.ServiceArn,
                    Tags = [..tags.Select(t => new Tag { Key = t.Key, Value = t.Value })]
                }, ct);
            }
        }
        else
        {
            log.Info("Service is using the same revision; will not be updated.");
        }

        log.Info(TaskDefinitionDiffer.Diff(existingTarget, newTaskDefinition ?? existingTarget));

        if (newTaskDefinition is not null)
        {
            deployment.Variables.Set(OutputRevisionVar, newTaskDefinition.Revision.ToString());
            deployment.Variables.Set(OutputFamilyVar, newTaskDefinition.Family);
        }

        await WaitForDeploymentAsync(updatedService, ct);
        await CheckTaskStatesAsync(updatedService, ct);
    }

    static RegisterTaskDefinitionRequest BuildRegisterRequest(TaskDefinition td, IReadOnlyList<KeyValuePair<string, string>> tags) => new()
    {
        Family = td.Family,
        ContainerDefinitions = td.ContainerDefinitions,
        Cpu = td.Cpu,
        Memory = td.Memory,
        ExecutionRoleArn = td.ExecutionRoleArn,
        TaskRoleArn = td.TaskRoleArn,
        NetworkMode = td.NetworkMode,
        RequiresCompatibilities = td.RequiresCompatibilities,
        Volumes = td.Volumes,
        PlacementConstraints = td.PlacementConstraints,
        ProxyConfiguration = td.ProxyConfiguration,
        IpcMode = td.IpcMode,
        PidMode = td.PidMode,
        RuntimePlatform = td.RuntimePlatform,
        EphemeralStorage = td.EphemeralStorage,
        Tags = [..tags.Select(t => new Tag { Key = t.Key, Value = t.Value })]
    };

    async Task WaitForDeploymentAsync(Service service, CancellationToken ct)
    {
        if (inputs.WaitOption == WaitOptionType.DontWait)
        {
            return;
        }
        if (service.DeploymentController?.Type is not null
            && service.DeploymentController.Type != DeploymentControllerType.ECS)
        {
            log.Verbose($"Deployment controller type is '{service.DeploymentController.Type}'; skipping deployment wait.");
            return;
        }

        var deadline = inputs.WaitTimeout.HasValue ? DateTime.UtcNow + inputs.WaitTimeout.Value : (DateTime?)null;

        while (true)
        {
            var resp = await ecs.DescribeServicesAsync(new DescribeServicesRequest
            {
                Cluster = inputs.ClusterName,
                Services = [inputs.ServiceName]
            }, ct);
            var current = resp.Services?.FirstOrDefault();
            var deploymentEntry = current?.Deployments?.OrderByDescending(d => d.UpdatedAt).FirstOrDefault();
            var rolloutState = deploymentEntry?.RolloutState?.Value;

            if (rolloutState == "COMPLETED")
            {
                log.Info("ECS service deployment completed.");
                return;
            }
            if (rolloutState == "FAILED")
            {
                throw new CommandException($"Reached deployment state: FAILED. Reason: {deploymentEntry?.RolloutStateReason}");
            }

            if (deadline.HasValue && DateTime.UtcNow >= deadline.Value)
            {
                throw new CommandException($"Timed out waiting for ECS service deployment. Last state: {rolloutState ?? "<unknown>"}.");
            }

            await Task.Delay(deploymentPollInterval(), ct);
        }
    }

    async Task CheckTaskStatesAsync(Service service, CancellationToken ct)
    {
        if (inputs.WaitOption == WaitOptionType.DontWait)
        {
            return;
        }
        if (service.DeploymentController?.Type is not null
            && service.DeploymentController.Type != DeploymentControllerType.ECS)
        {
            return;
        }

        var deadline = inputs.WaitTimeout.HasValue ? DateTime.UtcNow + inputs.WaitTimeout.Value : (DateTime?)null;

        while (true)
        {
            var listResp = await ecs.ListTasksAsync(new ListTasksRequest
            {
                Cluster = inputs.ClusterName,
                ServiceName = inputs.ServiceName
            }, ct);

            if (listResp.TaskArns is null || listResp.TaskArns.Count == 0)
            {
                return;
            }

            var describeResp = await ecs.DescribeTasksAsync(new DescribeTasksRequest
            {
                Cluster = inputs.ClusterName,
                Tasks = listResp.TaskArns
            }, ct);

            var statuses = describeResp.Tasks.Select(t => (t.TaskArn, t.LastStatus, t.StopCode, t.StoppedReason)).ToList();
            var stopped = statuses.Where(s => s.LastStatus == "STOPPED").ToList();
            var allRunning = statuses.All(s => s.LastStatus == "RUNNING");

            if (stopped.Any())
            {
                var msg = string.Join("; ", stopped.Select(s =>
                    $"Task {s.TaskArn} fails to run. StopCode {s.StopCode?.Value ?? "<none>"}. Reason: {s.StoppedReason ?? "<none>"}"));
                throw new CommandException(msg);
            }
            if (allRunning)
            {
                log.Info("All ECS tasks are in RUNNING state.");
                return;
            }

            if (deadline.HasValue && DateTime.UtcNow >= deadline.Value)
            {
                throw new CommandException("Timed out waiting for ECS tasks to reach RUNNING state.");
            }

            log.Info("One or more ECS tasks are still pending.");
            await Task.Delay(taskPollInterval(), ct);
        }
    }
}
