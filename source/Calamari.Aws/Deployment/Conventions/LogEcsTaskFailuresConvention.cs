using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.ECS;
using Amazon.ECS.Model;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using EcsTask = Amazon.ECS.Model.Task;
using Task = System.Threading.Tasks.Task;

namespace Calamari.Aws.Deployment.Conventions;

// Polls ECS tasks for the latest task-definition revision and logs StoppedCode/StoppedReason
// for any STOPPED task. CloudFormation rollback events don't surface per-task failure detail.
public class LogEcsTaskFailuresConvention : IInstallConvention
{
    readonly Func<IAmazonECS> ecsClientFactory;
    readonly string taskFamily;
    readonly string clusterName;
    readonly bool waitForComplete;
    readonly TimeSpan? waitTimeout;
    readonly TimeSpan pollInterval;
    readonly ILog log;

    public LogEcsTaskFailuresConvention(
        Func<IAmazonECS> ecsClientFactory,
        string taskFamily,
        string clusterName,
        bool waitForComplete,
        TimeSpan? waitTimeout,
        TimeSpan pollInterval,
        ILog log)
    {
        this.ecsClientFactory = ecsClientFactory;
        this.taskFamily = taskFamily;
        this.clusterName = clusterName;
        this.waitForComplete = waitForComplete;
        this.waitTimeout = waitTimeout;
        this.pollInterval = pollInterval;
        this.log = log;
    }

    public void Install(RunningDeployment deployment) => InstallAsync().GetAwaiter().GetResult();

    async Task InstallAsync()
    {
        if (!waitForComplete)
        {
            log.Verbose("WaitOption is dontWait; skipping ECS task diagnostics.");
            return;
        }

        using var ecsClient = ecsClientFactory();

        var taskDefinitionArn = await GetLatestTaskDefinitionArn(ecsClient);
        if (taskDefinitionArn == null)
        {
            log.Verbose($"Could not resolve latest task definition ARN for family \"{taskFamily}\"; skipping task-level diagnostics.");
            return;
        }

        var desiredCount = await GetServiceDesiredCount(ecsClient);
        if (desiredCount == 0)
        {
            log.Verbose($"Service \"{taskFamily}\" has DesiredCount=0; skipping task-level diagnostics.");
            return;
        }

        var tasks = await PollUntilFinal(ecsClient, taskDefinitionArn, desiredCount);

        if (tasks.All(t => t.LastStatus == "RUNNING"))
        {
            log.Info($"All {tasks.Count} ECS task(s) for \"{taskFamily}\" are RUNNING.");
            return;
        }

        foreach (var task in tasks.Where(t => t.LastStatus == "STOPPED"))
        {
            log.Warn($"- Task \"{task.TaskArn}\" fails to run. StoppedCode: {task.StopCode?.Value}. Reason: \"{task.StoppedReason}\".");
        }
    }

    async Task<List<EcsTask>> PollUntilFinal(IAmazonECS ecsClient, string taskDefinitionArn, int? desiredCount)
    {
        var startedAt = DateTime.UtcNow;
        while (true)
        {
            if (waitTimeout.HasValue && DateTime.UtcNow - startedAt > waitTimeout.Value)
                throw new TimeoutException($"Timed out after {waitTimeout.Value} waiting for ECS tasks in family \"{taskFamily}\" to reach a final state.");

            var tasks = await GetLatestRevisionTasks(ecsClient, taskDefinitionArn);
            if (tasks.Count > 0 && tasks.All(IsFinalState) && (!desiredCount.HasValue || tasks.Count == desiredCount.Value))
                return tasks;

            log.Info("One or more ECS tasks are still pending.");
            await Task.Delay(pollInterval);
        }
    }

    static bool IsFinalState(EcsTask task) =>
        task.LastStatus is "RUNNING" or "STOPPED";

    async Task<int?> GetServiceDesiredCount(IAmazonECS ecsClient)
    {
        try
        {
            var response = await ecsClient.DescribeServicesAsync(new DescribeServicesRequest
            {
                Cluster = clusterName,
                Services = [taskFamily]
            });
            return response.Services?.FirstOrDefault()?.DesiredCount;
        }
        catch
        {
            return null;
        }
    }

    async Task<string> GetLatestTaskDefinitionArn(IAmazonECS ecsClient)
    {
        try
        {
            var response = await ecsClient.DescribeTaskDefinitionAsync(new DescribeTaskDefinitionRequest { TaskDefinition = taskFamily });
            return response.TaskDefinition?.TaskDefinitionArn;
        }
        catch
        {
            return null;
        }
    }

    async Task<List<EcsTask>> GetLatestRevisionTasks(IAmazonECS ecsClient, string taskDefinitionArn)
    {
        try
        {
            var listResponse = await ecsClient.ListTasksAsync(new ListTasksRequest
            {
                Cluster = clusterName,
                Family = taskFamily
            });

            if (listResponse.TaskArns == null || listResponse.TaskArns.Count == 0)
            {
                return [];
            }

            var describeResponse = await ecsClient.DescribeTasksAsync(new DescribeTasksRequest
            {
                Cluster = clusterName,
                Tasks = listResponse.TaskArns
            });

            return describeResponse.Tasks?.Where(t => t.TaskDefinitionArn == taskDefinitionArn).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }
}
