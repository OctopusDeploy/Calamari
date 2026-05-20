using System;
using System.Linq;
using System.Threading;
using Amazon.ECS;
using Amazon.ECS.Model;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.Aws.Ecs;
using Task = System.Threading.Tasks.Task;

namespace Calamari.Aws.Integration.Ecs;

public class EcsPostDeployWatcher
{
    readonly IAmazonECS ecs;
    readonly ILog log;
    readonly string clusterName;
    readonly string serviceName;
    readonly WaitOption waitOption;
    readonly Func<TimeSpan> deploymentPollInterval;
    readonly Func<TimeSpan> taskPollInterval;

    public EcsPostDeployWatcher(
        IAmazonECS ecs,
        ILog log,
        string clusterName,
        string serviceName,
        WaitOption waitOption,
        Func<TimeSpan> deploymentPollInterval = null,
        Func<TimeSpan> taskPollInterval = null)
    {
        this.ecs = ecs;
        this.log = log;
        this.clusterName = clusterName;
        this.serviceName = serviceName;
        this.waitOption = waitOption;
        this.deploymentPollInterval = deploymentPollInterval ?? (() => TimeSpan.FromSeconds(3));
        this.taskPollInterval = taskPollInterval ?? (() => TimeSpan.FromSeconds(10));
    }

    public async Task WaitAsync(Service service, CancellationToken ct = default)
    {
        if (waitOption.Type == WaitType.DontWait)
        {
            return;
        }

        if (service.DeploymentController?.Type is not null
            && service.DeploymentController.Type != DeploymentControllerType.ECS)
        {
            log.Verbose($"Deployment controller type is '{service.DeploymentController.Type}'; skipping service and task checks.");
            return;
        }

        await WaitForDeploymentAsync(ct);
        await WaitForTaskStatesAsync(ct);
    }

    async Task WaitForDeploymentAsync(CancellationToken ct)
    {
        var timeout = GetTimeout();

        log.Info($"Waiting for ECS service '{serviceName}' deployment to reach COMPLETED.");
        string lastReportedState = null;

        while (true)
        {
            var response = await ecs.DescribeServicesAsync(new DescribeServicesRequest
            {
                Cluster = clusterName,
                Services = [serviceName]
            }, ct);

            var service = response.Services?.FirstOrDefault();
            if (service is null)
            {
                throw new CommandException($"Service '{serviceName}' was not found in cluster '{clusterName}' while waiting for deployment to complete.");
            }

            var primaryDeployment = service.Deployments.FirstOrDefault(d => d.Status == "PRIMARY");
            var rolloutState = primaryDeployment?.RolloutState?.Value;
            if (primaryDeployment is not null)
            {
                switch (rolloutState)
                {
                    case "COMPLETED":
                        log.Info("ECS service deployment completed.");
                        return;
                    case "FAILED":
                        throw new CommandException($"Reached deployment state: FAILED. Reason: {primaryDeployment.RolloutStateReason}");
                }

                if (rolloutState != lastReportedState)
                {
                    lastReportedState = rolloutState;
                    log.Info($"Service rollout state: {lastReportedState}.");
                }
            }

            if (timeout.HasValue && DateTime.UtcNow >= timeout.Value)
            {
                var events = service.Events?.Take(5).ToList() ?? [];
                if (events.Count > 0)
                {
                    log.Info("Recent ECS service events:");
                    foreach (var e in events)
                    {
                        log.Info($"  [{e.CreatedAt:o}] {e.Message}");
                    }
                }

                throw new CommandException("Timed out waiting for ECS service deployment.");
            }

            await Task.Delay(deploymentPollInterval(), ct);
        }
    }

    async Task WaitForTaskStatesAsync(CancellationToken ct)
    {
        var timeout = GetTimeout();

        while (true)
        {
            var listResp = await ecs.ListTasksAsync(new ListTasksRequest
            {
                Cluster = clusterName,
                ServiceName = serviceName
            }, ct);

            if (listResp.TaskArns is null || listResp.TaskArns.Count == 0)
            {
                log.Info($"No tasks found for service '{serviceName}'; skipping task check.");
                return;
            }

            var describeResponse = await ecs.DescribeTasksAsync(new DescribeTasksRequest
            {
                Cluster = clusterName,
                Tasks = listResp.TaskArns
            }, ct);

            var stoppedTasks = describeResponse.Tasks.Where(t => t.LastStatus == "STOPPED").ToArray();
            var allTasksRunning = describeResponse.Tasks.All(t => t.LastStatus == "RUNNING");

            if (stoppedTasks.Length != 0)
            {
                log.Info("Stopped ECS tasks");
                foreach (var stoppedTask in stoppedTasks)
                {
                    log.Info($"  Task: {stoppedTask.TaskArn} StopCode: {stoppedTask.StopCode?.Value ?? "<none>"} StoppedReason: {stoppedTask.StoppedReason ?? "<none>"}");
                }

                throw new CommandException("ECS task check failed as some tasks are stopped.");
            }

            if (allTasksRunning)
            {
                log.Info("All ECS tasks are in RUNNING state.");
                return;
            }

            if (timeout.HasValue && DateTime.UtcNow >= timeout.Value)
            {
                throw new CommandException("Timed out waiting for all ECS tasks to reach RUNNING state.");
            }

            await Task.Delay(taskPollInterval(), ct);
        }
    }

    DateTime? GetTimeout()
    {
        var span = waitOption.GetTimeoutSpan();
        return span.HasValue ? DateTime.UtcNow + span.Value : null;
    }
}
