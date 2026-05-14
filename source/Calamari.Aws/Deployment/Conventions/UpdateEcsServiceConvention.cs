using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Amazon.ECS;
using Amazon.ECS.Model;
using Calamari.Aws.Integration.Ecs;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Task = System.Threading.Tasks.Task;

namespace Calamari.Aws.Deployment.Conventions;

public class UpdateEcsServiceConvention(
    IAmazonECS ecs,
    ILog log,
    AwsEnvironmentGeneration environment,
    string clusterName,
    string serviceName,
    string templateTaskDefinitionName,
    string targetTaskDefinitionName,
    List<EcsContainerUpdate> containers,
    List<KeyValuePair<string, string>> tags,
    WaitOptionType waitOption,
    TimeSpan? waitTimeout,
    Func<TimeSpan> deploymentPollInterval = null,
    Func<TimeSpan> taskPollInterval = null)
    : IInstallConvention
{
    readonly EcsPostDeployWatcher watcher = new(ecs, log, clusterName, serviceName, waitOption, waitTimeout, deploymentPollInterval, taskPollInterval);

    public void Install(RunningDeployment deployment) => InstallAsync(deployment).GetAwaiter().GetResult();

    async Task InstallAsync(RunningDeployment deployment)
    {
        var ct = CancellationToken.None;

        DescribeTaskDefinitionResponse templateResp;
        try
        {
            templateResp = await ecs.DescribeTaskDefinitionAsync(
                new DescribeTaskDefinitionRequest { TaskDefinition = templateTaskDefinitionName, Include = ["TAGS"] }, ct);
        }
        catch (ClientException)
        {
            throw new CommandException($"Template task definition '{templateTaskDefinitionName}' not found.");
        }
        var template = templateResp.TaskDefinition;

        // SPF behavior to check if the target task definition family exists before applying the update
        // Only needed if the target is different from the source
        // Without this check, AWS will just create any task definition family it is given
        if (targetTaskDefinitionName != templateTaskDefinitionName)
        {
            try
            {
                await ecs.DescribeTaskDefinitionAsync(
                    new DescribeTaskDefinitionRequest { TaskDefinition = targetTaskDefinitionName }, ct);
            }
            catch (ClientException)
            {
                throw new CommandException($"Existing destination task definition '{targetTaskDefinitionName}' not found.");
            }
        }

        var taskDefTags = EcsDefaultTags.MergeAndDeduplicateTags(deployment.Variables, tags, templateResp.Tags);
        var registerRequest = RegisterTaskDefinitionRequestFactory.FromTaskDefinition(template, targetTaskDefinitionName, containers, taskDefTags);
        var registerResp = await ecs.RegisterTaskDefinitionAsync(registerRequest, ct);
        var registeredTaskDef = registerResp.TaskDefinition;

        var serviceResp = await ecs.DescribeServicesAsync(new DescribeServicesRequest
        {
            Cluster = clusterName,
            Services = [serviceName],
            Include = ["TAGS"]
        }, ct);
        var existingService = serviceResp.Services?.FirstOrDefault()
            ?? throw new CommandException($"Service '{serviceName}' not found in cluster '{clusterName}'.");

        var updateResp = await ecs.UpdateServiceAsync(new UpdateServiceRequest
        {
            Cluster = clusterName,
            Service = serviceName,
            TaskDefinition = $"{registeredTaskDef.Family}:{registeredTaskDef.Revision}",
            ForceNewDeployment = true
        }, ct);
        var updatedService = updateResp.Service;

        var serviceTags = EcsDefaultTags.MergeAndDeduplicateTags(deployment.Variables, tags, existingService.Tags);
        if (serviceTags.Count > 0)
        {
            await ecs.TagResourceAsync(new TagResourceRequest
            {
                ResourceArn = updatedService.ServiceArn,
                Tags = serviceTags.Select(t => new Tag { Key = t.Key, Value = t.Value }).ToList()
            }, ct);
        }

        SetOutputVariable(deployment.Variables, "TaskDefinitionFamily", registeredTaskDef.Family);
        SetOutputVariable(deployment.Variables, "TaskDefinitionRevision", registeredTaskDef.Revision.ToString());
        SetOutputVariable(deployment.Variables, "ClusterName", clusterName);
        SetOutputVariable(deployment.Variables, "ServiceName", serviceName);
        SetOutputVariable(deployment.Variables, "Region", environment.AwsRegion.SystemName);

        await watcher.WaitAsync(updatedService, ct);
    }

    void SetOutputVariable(IVariables variables, string name, string value)
    {
        log.Info($"Saving variable \"Octopus.Action[{variables["Octopus.Action.Name"]}].Output.{name}\"");
        log.SetOutputVariable(name, value, variables);
    }
}
