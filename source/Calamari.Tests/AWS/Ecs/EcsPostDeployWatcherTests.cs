using System;
using System.Threading;
using Amazon.ECS;
using Amazon.ECS.Model;
using Calamari.Aws.Integration.Ecs;
using Calamari.Common.Commands;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Calamari.Contracts.Aws.Ecs;
using Task = System.Threading.Tasks.Task;
using EcsTask = Amazon.ECS.Model.Task;
using EcsDeployment = Amazon.ECS.Model.Deployment;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class EcsPostDeployWatcherTests
{
    IAmazonECS ecs;

    [SetUp]
    public void SetUp()
    {
        ecs = Substitute.For<IAmazonECS>();
    }

    EcsPostDeployWatcher Watcher(WaitType waitType = WaitType.WaitUntilCompleted, string timeout = null) =>
        new(
            ecs,
            new InMemoryLog(),
            clusterName: "cluster-x",
            serviceName: "svc-x",
            waitOption: new WaitOption { Type = waitType, Timeout = timeout },
            deploymentPollInterval: () => TimeSpan.Zero,
            taskPollInterval: () => TimeSpan.Zero);

    [Test]
    public async Task DontWaitSkipsPolling()
    {
        await Watcher(WaitType.DontWait).WaitAsync(Service());

        await ecs.DidNotReceive().DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>());
        await ecs.DidNotReceive().ListTasksAsync(Arg.Any<ListTasksRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AllTasksRunningCompletesSuccessfully()
    {
        ecs.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>())
           .Returns(ServicesWithRollout(DeploymentRolloutState.COMPLETED));
        ecs.ListTasksAsync(Arg.Any<ListTasksRequest>(), Arg.Any<CancellationToken>())
           .Returns(new ListTasksResponse { TaskArns = ["arn:task/1"] });
        ecs.DescribeTasksAsync(Arg.Any<DescribeTasksRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeTasksResponse
           {
               Tasks = [new EcsTask { TaskArn = "arn:task/1", LastStatus = "RUNNING" }]
           });

        await Watcher().WaitAsync(Service());
    }

    [Test]
    public async Task DeploymentFailedThrowsWithRolloutReason()
    {
        ecs.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>())
           .Returns(ServicesWithRollout(DeploymentRolloutState.FAILED, "task failed to start"));

        var act = async () => await Watcher().WaitAsync(Service());

        await act.Should().ThrowAsync<CommandException>().WithMessage("*FAILED*task failed to start*");
    }

    [Test]
    public async Task AnyTaskStoppedThrows()
    {
        ecs.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>())
           .Returns(ServicesWithRollout(DeploymentRolloutState.COMPLETED));
        ecs.ListTasksAsync(Arg.Any<ListTasksRequest>(), Arg.Any<CancellationToken>())
           .Returns(new ListTasksResponse { TaskArns = ["arn:task/1"] });
        ecs.DescribeTasksAsync(Arg.Any<DescribeTasksRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeTasksResponse
           {
               Tasks =
               [
                   new EcsTask
                   {
                       TaskArn = "arn:task/1",
                       LastStatus = "STOPPED",
                       StopCode = TaskStopCode.EssentialContainerExited,
                       StoppedReason = "container exited with code 1"
                   }
               ]
           });

        var act = async () => await Watcher().WaitAsync(Service());

        await act.Should().ThrowAsync<CommandException>().WithMessage("*ECS task check failed*");
    }

    [Test]
    public async Task NoServiceWhilePollingThrows()
    {
        ecs.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeServicesResponse { Services = [] });

        var act = async () => await Watcher().WaitAsync(Service());

        await act.Should().ThrowAsync<CommandException>().WithMessage("*svc-x*not found*cluster-x*");
    }

    [Test]
    public async Task DeploymentDoesNotCompleteTimesOut()
    {
        ecs.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>())
           .Returns(ServicesWithRollout(DeploymentRolloutState.IN_PROGRESS));

        var act = async () => await Watcher(WaitType.WaitWithTimeout, "0").WaitAsync(Service());

        await act.Should().ThrowAsync<CommandException>().WithMessage("*Timed out*deployment*");
    }

    [Test]
    public async Task TasksStuckPendingTimesOut()
    {
        ecs.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>())
           .Returns(ServicesWithRollout(DeploymentRolloutState.COMPLETED));
        ecs.ListTasksAsync(Arg.Any<ListTasksRequest>(), Arg.Any<CancellationToken>())
           .Returns(new ListTasksResponse { TaskArns = ["arn:task/1"] });
        ecs.DescribeTasksAsync(Arg.Any<DescribeTasksRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeTasksResponse
           {
               Tasks = [new EcsTask { TaskArn = "arn:task/1", LastStatus = "PENDING" }]
           });

        var act = async () => await Watcher(WaitType.WaitWithTimeout, "0").WaitAsync(Service());

        await act.Should().ThrowAsync<CommandException>().WithMessage("*tasks to reach RUNNING*");
    }

    static Service Service(DeploymentControllerType controllerType = null) => new()
    {
        ServiceName = "svc-x",
        DeploymentController = controllerType is null ? null : new DeploymentController { Type = controllerType }
    };

    static DescribeServicesResponse ServicesWithRollout(DeploymentRolloutState state, string reason = null) => new()
    {
        Services =
        [
            new Service
            {
                Deployments =
                [
                    new EcsDeployment
                    {
                        Status = "PRIMARY",
                        RolloutState = state,
                        RolloutStateReason = reason,
                        UpdatedAt = DateTime.UtcNow
                    }
                ]
            }
        ]
    };
}
