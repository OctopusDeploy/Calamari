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
            log.Verbose("Wait option set to don't wait so skip ECS task checks.");
            return;
        }

        using var ecsClient = ecsClientFactory();

        var taskDefinitionArn = await GetLatestTaskDefinitionArn(ecsClient);
        if (taskDefinitionArn == null)
        {
            log.Verbose($"Could not resolve latest task definition ARN for family \"{taskFamily}\"; skipping task-level diagnostics.");
            return;
        }

        var tasks = await PollUntilFinal(ecsClient, taskDefinitionArn);

        if (tasks.All(t => t.LastStatus == "RUNNING"))
        {
            log.Info($"All {tasks.Count} ECS task(s) for \"{taskFamily}\" are RUNNING.");
            return;
        }

        var stoppedTasks = tasks.Where(t => t.LastStatus == "STOPPED").ToList();
        if (stoppedTasks.Count > 0)
        {
            log.Warn(string.Join("\n", stoppedTasks.Select(FormatStoppedTask)));
        }
    }

    async Task<List<EcsTask>> PollUntilFinal(IAmazonECS ecsClient, string taskDefinitionArn)
    {
        var startedAt = DateTime.UtcNow;
        (int pending, int running, int stopped)? lastObserved = null;
        var loggedStoppedArns = new HashSet<string>();
        while (true)
        {
            if (waitTimeout.HasValue && DateTime.UtcNow - startedAt > waitTimeout.Value)
            {
                throw new TimeoutException($"Timed out after {waitTimeout.Value} waiting for ECS tasks in family \"{taskFamily}\" to reach a final state.");
            }

            var tasks = await DescribeTasksForRevision(ecsClient, taskDefinitionArn);
            if (tasks.Count > 0 && tasks.All(IsFinalState))
            {
                return tasks;
            }

            var observed = (
                pending: tasks.Count(t => !IsFinalState(t)),
                running: tasks.Count(t => t.LastStatus == "RUNNING"),
                stopped: tasks.Count(t => t.LastStatus == "STOPPED"));
            if (observed != lastObserved)
            {
                log.Info($"ECS tasks for \"{taskFamily}\": {observed.pending} pending, {observed.running} running, {observed.stopped} stopped.");
                lastObserved = observed;
            }

            await LogNewStoppedTasks(ecsClient, taskDefinitionArn, loggedStoppedArns);

            await Task.Delay(pollInterval);
        }
    }

    async Task LogNewStoppedTasks(IAmazonECS ecsClient, string taskDefinitionArn, HashSet<string> loggedArns)
    {
        foreach (var task in await DescribeTasksForRevision(ecsClient, taskDefinitionArn, DesiredStatus.STOPPED))
        {
            if (loggedArns.Add(task.TaskArn))
            {
                log.Info(FormatStoppedTask(task));
            }
        }
    }

    static bool IsFinalState(EcsTask task) =>
        task.LastStatus is "RUNNING" or "STOPPED";

    static string FormatStoppedTask(EcsTask task) =>
        $"- Task \"{task.TaskArn}\" failed to run. StoppedCode: {task.StopCode?.Value}. Reason: \"{task.StoppedReason}\".";

    async Task<string> GetLatestTaskDefinitionArn(IAmazonECS ecsClient)
    {
        try
        {
            var response = await ecsClient.DescribeTaskDefinitionAsync(new DescribeTaskDefinitionRequest { TaskDefinition = taskFamily });
            return response.TaskDefinition?.TaskDefinitionArn;
        }
        catch (Exception ex)
        {
            log.Verbose($"Failed to describe task definition \"{taskFamily}\": {ex.Message}");
            return null;
        }
    }

    async Task<List<EcsTask>> DescribeTasksForRevision(IAmazonECS ecsClient, string taskDefinitionArn, DesiredStatus desiredStatus = null)
    {
        try
        {
            var listResponse = await ecsClient.ListTasksAsync(new ListTasksRequest
            {
                Cluster = clusterName,
                Family = taskFamily,
                DesiredStatus = desiredStatus
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
        catch (Exception ex)
        {
            log.Verbose($"Failed to list/describe tasks for family \"{taskFamily}\" in cluster \"{clusterName}\": {ex.Message}");
            return [];
        }
    }
}
