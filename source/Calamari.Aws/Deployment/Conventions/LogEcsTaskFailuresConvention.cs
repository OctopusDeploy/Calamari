using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.ECS;
using Amazon.ECS.Model;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using EcsTask = Amazon.ECS.Model.Task;
using Task = System.Threading.Tasks.Task;

namespace Calamari.Aws.Deployment.Conventions;

// Post-deploy: logs StoppedCode and StoppedReason for any tasks of the latest task-definition
// revision that are in STOPPED state. CloudFormation rollback events don't surface per-task
// failure detail, so this fills the diagnostic gap.
public class LogEcsTaskFailuresConvention : IInstallConvention
{
    readonly AwsEnvironmentGeneration environment;
    readonly string taskFamily;
    readonly string clusterName;
    readonly ILog log;

    public LogEcsTaskFailuresConvention(AwsEnvironmentGeneration environment, string taskFamily, string clusterName, ILog log)
    {
        this.environment = environment;
        this.taskFamily = taskFamily;
        this.clusterName = clusterName;
        this.log = log;
    }

    public void Install(RunningDeployment deployment) => InstallAsync().GetAwaiter().GetResult();

    async Task InstallAsync()
    {
        using var ecsClient = ClientHelpers.CreateEcsClient(environment);

        var taskDefinitionArn = await GetLatestTaskDefinitionArn(ecsClient);
        if (taskDefinitionArn == null)
        {
            log.Verbose($"Could not resolve latest task definition ARN for family \"{taskFamily}\"; skipping task-level diagnostics.");
            return;
        }

        var tasks = await GetLatestRevisionTasks(ecsClient, taskDefinitionArn);
        if (tasks.Count == 0)
        {
            log.Verbose($"No ECS tasks found for family \"{taskFamily}\" at the latest revision; skipping task-level diagnostics.");
            return;
        }

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
