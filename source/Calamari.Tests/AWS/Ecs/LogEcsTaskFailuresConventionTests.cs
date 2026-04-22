using System;
using System.Linq;
using Amazon.ECS;
using Amazon.ECS.Model;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using EcsTask = Amazon.ECS.Model.Task;
using Task = System.Threading.Tasks.Task;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class LogEcsTaskFailuresConventionTests
{
    const string TaskFamily = "my-family";
    const string ClusterName = "my-cluster";
    const string TaskDefinitionArn = "arn:aws:ecs:us-east-1:123:task-definition/my-family:1";
    const string TaskArn = "arn:aws:ecs:us-east-1:123:task/abc";

    static readonly TimeSpan FastPoll = TimeSpan.FromMilliseconds(10);

    [Test]
    public void LogsStoppedReason_ForFailedTasks()
    {
        var ecsClient = Substitute.For<IAmazonECS>();
        ConfigureTaskDefinition(ecsClient);
        ConfigureDesiredCount(ecsClient, 1);
        ConfigureTasks(ecsClient, new EcsTask
        {
            TaskArn = TaskArn,
            TaskDefinitionArn = TaskDefinitionArn,
            LastStatus = "STOPPED",
            StopCode = new TaskStopCode("TaskFailedToStart"),
            StoppedReason = "ResourceInitializationError: failed to pull image"
        });

        var log = new InMemoryLog();
        var convention = Create(ecsClient, log, waitTimeout: null);

        convention.Install(new RunningDeployment(new CalamariVariables()));

        log.MessagesWarnFormatted.Should().ContainSingle()
           .Which.Should().Contain(TaskArn)
                         .And.Contain("TaskFailedToStart")
                         .And.Contain("ResourceInitializationError: failed to pull image");
    }

    [Test]
    public void KeepsPolling_UntilTasksReachFinalState()
    {
        var ecsClient = Substitute.For<IAmazonECS>();
        ConfigureTaskDefinition(ecsClient);
        ConfigureDesiredCount(ecsClient, 1);
        ecsClient.ListTasksAsync(Arg.Any<ListTasksRequest>())
                 .Returns(Task.FromResult(new ListTasksResponse { TaskArns = [TaskArn] }));

        var describeCallCount = 0;
        ecsClient.DescribeTasksAsync(Arg.Any<DescribeTasksRequest>())
                 .Returns(_ =>
                          {
                              describeCallCount++;
                              var status = describeCallCount <= 2 ? "PENDING" : "RUNNING";
                              return Task.FromResult(new DescribeTasksResponse
                              {
                                  Tasks = [new EcsTask
                                  {
                                      TaskArn = TaskArn,
                                      TaskDefinitionArn = TaskDefinitionArn,
                                      LastStatus = status
                                  }]
                              });
                          });

        var log = new InMemoryLog();
        var convention = Create(ecsClient, log, waitTimeout: null);

        convention.Install(new RunningDeployment(new CalamariVariables()));

        describeCallCount.Should().BeGreaterThanOrEqualTo(3);
        log.MessagesInfoFormatted.Should().Contain(m => m.Contains("are RUNNING"));
        log.MessagesWarnFormatted.Should().BeEmpty();
    }

    [Test]
    public void ThrowsTimeoutException_WhenTasksStayPendingPastTimeout()
    {
        var ecsClient = Substitute.For<IAmazonECS>();
        ConfigureTaskDefinition(ecsClient);
        ConfigureDesiredCount(ecsClient, 1);
        ConfigureTasks(ecsClient, new EcsTask
        {
            TaskArn = TaskArn,
            TaskDefinitionArn = TaskDefinitionArn,
            LastStatus = "PENDING"
        });

        var log = new InMemoryLog();
        var convention = Create(ecsClient, log, waitTimeout: TimeSpan.FromMilliseconds(150), pollInterval: TimeSpan.FromMilliseconds(25));

        var ex = Assert.Throws<TimeoutException>(() => convention.Install(new RunningDeployment(new CalamariVariables())));
        ex!.Message.Should().Contain(TaskFamily);
    }

    [Test]
    public void SkipsTaskPolling_WhenServiceDesiredCountIsZero()
    {
        var ecsClient = Substitute.For<IAmazonECS>();
        ConfigureTaskDefinition(ecsClient);
        ConfigureDesiredCount(ecsClient, 0);

        var log = new InMemoryLog();
        var convention = Create(ecsClient, log, waitTimeout: null);

        convention.Install(new RunningDeployment(new CalamariVariables()));

        ecsClient.DidNotReceive().ListTasksAsync(Arg.Any<ListTasksRequest>());
        ecsClient.DidNotReceive().DescribeTasksAsync(Arg.Any<DescribeTasksRequest>());
    }

    static LogEcsTaskFailuresConvention Create(
        IAmazonECS ecsClient,
        InMemoryLog log,
        TimeSpan? waitTimeout,
        TimeSpan? pollInterval = null) =>
        new(() => ecsClient,
            TaskFamily,
            ClusterName,
            waitForComplete: true,
            waitTimeout: waitTimeout,
            pollInterval: pollInterval ?? FastPoll,
            log);

    static void ConfigureTaskDefinition(IAmazonECS ecsClient) =>
        ecsClient.DescribeTaskDefinitionAsync(Arg.Any<DescribeTaskDefinitionRequest>())
                 .Returns(Task.FromResult(new DescribeTaskDefinitionResponse
                 {
                     TaskDefinition = new TaskDefinition { TaskDefinitionArn = TaskDefinitionArn }
                 }));

    static void ConfigureDesiredCount(IAmazonECS ecsClient, int desiredCount) =>
        ecsClient.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>())
                 .Returns(Task.FromResult(new DescribeServicesResponse
                 {
                     Services = [new Service { DesiredCount = desiredCount }]
                 }));

    static void ConfigureTasks(IAmazonECS ecsClient, params EcsTask[] tasks)
    {
        ecsClient.ListTasksAsync(Arg.Any<ListTasksRequest>())
                 .Returns(Task.FromResult(new ListTasksResponse
                 {
                     TaskArns = tasks.Select(t => t.TaskArn).ToList()
                 }));
        ecsClient.DescribeTasksAsync(Arg.Any<DescribeTasksRequest>())
                 .Returns(Task.FromResult(new DescribeTasksResponse
                 {
                     Tasks = tasks.ToList()
                 }));
    }
}
