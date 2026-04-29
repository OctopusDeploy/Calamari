using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.ECS;
using Amazon.ECS.Model;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.Ecs.Update;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Task = System.Threading.Tasks.Task;
using EcsTask = Amazon.ECS.Model.Task;
using EcsDeployment = Amazon.ECS.Model.Deployment;

namespace Calamari.Tests.AWS.Ecs.Update;

[TestFixture]
public class UpdateEcsServiceConventionTests
{
    static EcsUpdateServiceInputs MakeInputs(WaitOptionType waitOption = WaitOptionType.DontWait) => new(
        ClusterName: "cluster-x",
        ServiceName: "svc-x",
        TargetTaskDefinitionName: "fam-x",
        TemplateTaskDefinitionName: "fam-x",
        Containers: [new EcsContainerUpdate("web", "new:2", EnvVarAction.None, EnvFileAction.None)],
        UserTags: [],
        WaitOption: waitOption,
        WaitTimeout: null);

    static IAmazonECS CreateEcs() => Substitute.For<IAmazonECS>();

    [Test]
    public async Task RunsRegisterAndUpdate_WhenTemplateDiffersFromTarget()
    {
        var ecs = CreateEcs();
        // Each DescribeTaskDefinition call returns a fresh TaskDefinition instance, matching
        // real AWS behaviour. The mutator mutates the template in place; if template and target
        // shared a reference, equality would always succeed.
        ecs.DescribeTaskDefinitionAsync(Arg.Any<DescribeTaskDefinitionRequest>(), Arg.Any<CancellationToken>())
           .Returns(_ => new DescribeTaskDefinitionResponse
           {
               TaskDefinition = new TaskDefinition
               {
                   Family = "fam-x",
                   ContainerDefinitions = [new ContainerDefinition { Name = "web", Image = "old:1" }]
               }
           });

        ecs.RegisterTaskDefinitionAsync(Arg.Any<RegisterTaskDefinitionRequest>(), Arg.Any<CancellationToken>())
           .Returns(new RegisterTaskDefinitionResponse
           {
               TaskDefinition = new TaskDefinition { Family = "fam-x", Revision = 7 }
           });

        ecs.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeServicesResponse
           {
               Services = [new Service { ServiceName = "svc-x", TaskDefinition = "fam-x:6" }]
           });

        ecs.UpdateServiceAsync(Arg.Any<UpdateServiceRequest>(), Arg.Any<CancellationToken>())
           .Returns(new UpdateServiceResponse
           {
               Service = new Service { ServiceName = "svc-x", TaskDefinition = "fam-x:7" }
           });

        var convention = new UpdateEcsServiceConvention(ecs, MakeInputs(), [], new InMemoryLog());
        var deployment = new RunningDeployment(new CalamariVariables());

        await convention.InstallAsync(deployment);

        await ecs.Received(1).RegisterTaskDefinitionAsync(
            Arg.Is<RegisterTaskDefinitionRequest>(r => r.Family == "fam-x"),
            Arg.Any<CancellationToken>());
        await ecs.Received(1).UpdateServiceAsync(
            Arg.Is<UpdateServiceRequest>(r => r.ForceNewDeployment == true),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SkipsRegisterAndUpdate_WhenTemplateMatchesTargetAndServiceOnSameRevision()
    {
        var template = new TaskDefinition
        {
            Family = "fam-x",
            Revision = 6,
            ContainerDefinitions = [new ContainerDefinition { Name = "web", Image = "new:2" }]
        };
        var ecs = CreateEcs();

        ecs.DescribeTaskDefinitionAsync(Arg.Any<DescribeTaskDefinitionRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeTaskDefinitionResponse { TaskDefinition = template });

        ecs.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeServicesResponse
           {
               Services = [new Service { ServiceName = "svc-x", TaskDefinition = "fam-x:6" }]
           });

        var convention = new UpdateEcsServiceConvention(ecs, MakeInputs(), [], new InMemoryLog());
        await convention.InstallAsync(new RunningDeployment(new CalamariVariables()));

        await ecs.DidNotReceive().RegisterTaskDefinitionAsync(Arg.Any<RegisterTaskDefinitionRequest>(), Arg.Any<CancellationToken>());
        await ecs.DidNotReceive().UpdateServiceAsync(Arg.Any<UpdateServiceRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Throws_WhenTemplateTaskDefinitionDescribeFails()
    {
        var ecs = CreateEcs();
        ecs.DescribeTaskDefinitionAsync(Arg.Any<DescribeTaskDefinitionRequest>(), Arg.Any<CancellationToken>())
           .Throws(new ClientException("Unable to describe task definition"));

        var convention = new UpdateEcsServiceConvention(ecs, MakeInputs(), [], new InMemoryLog());

        var act = async () => await convention.InstallAsync(new RunningDeployment(new CalamariVariables()));
        await act.Should().ThrowAsync<ClientException>();
    }

    [Test]
    public async Task TaskCheck_StoppedTask_ThrowsWithStoppedReason()
    {
        var template = new TaskDefinition
        {
            Family = "fam-x",
            ContainerDefinitions = [new ContainerDefinition { Name = "web", Image = "old:1" }]
        };
        var ecs = CreateEcs();

        ecs.DescribeTaskDefinitionAsync(Arg.Any<DescribeTaskDefinitionRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeTaskDefinitionResponse { TaskDefinition = template });

        ecs.RegisterTaskDefinitionAsync(Arg.Any<RegisterTaskDefinitionRequest>(), Arg.Any<CancellationToken>())
           .Returns(new RegisterTaskDefinitionResponse { TaskDefinition = new TaskDefinition { Family = "fam-x", Revision = 7 } });

        ecs.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeServicesResponse
           {
               Services = [new Service
               {
                   ServiceName = "svc-x",
                   TaskDefinition = "fam-x:1",
                   Deployments = [new EcsDeployment { RolloutState = DeploymentRolloutState.COMPLETED, UpdatedAt = DateTime.UtcNow }]
               }]
           });

        ecs.UpdateServiceAsync(Arg.Any<UpdateServiceRequest>(), Arg.Any<CancellationToken>())
           .Returns(new UpdateServiceResponse
           {
               Service = new Service
               {
                   ServiceName = "svc-x",
                   TaskDefinition = "fam-x:7",
                   Deployments = [new EcsDeployment { RolloutState = DeploymentRolloutState.COMPLETED, UpdatedAt = DateTime.UtcNow }]
               }
           });

        ecs.ListTasksAsync(Arg.Any<ListTasksRequest>(), Arg.Any<CancellationToken>())
           .Returns(new ListTasksResponse { TaskArns = ["arn:aws:ecs:::task/abc"] });

        ecs.DescribeTasksAsync(Arg.Any<DescribeTasksRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeTasksResponse
           {
               Tasks = [new EcsTask
               {
                   TaskArn = "arn:aws:ecs:::task/abc",
                   LastStatus = "STOPPED",
                   StopCode = TaskStopCode.EssentialContainerExited,
                   StoppedReason = "container exited with code 1"
               }]
           });

        var convention = new UpdateEcsServiceConvention(
            ecs, MakeInputs(WaitOptionType.WaitUntilCompleted), [], new InMemoryLog(),
            deploymentPollInterval: () => TimeSpan.Zero,
            taskPollInterval: () => TimeSpan.Zero);

        var act = async () => await convention.InstallAsync(new RunningDeployment(new CalamariVariables()));
        await act.Should().ThrowAsync<CommandException>().WithMessage("*container exited with code 1*");
    }

    [Test]
    public async Task DeploymentWait_FAILED_Throws()
    {
        var template = new TaskDefinition
        {
            Family = "fam-x",
            ContainerDefinitions = [new ContainerDefinition { Name = "web", Image = "old:1" }]
        };
        var ecs = CreateEcs();

        ecs.DescribeTaskDefinitionAsync(Arg.Any<DescribeTaskDefinitionRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeTaskDefinitionResponse { TaskDefinition = template });

        ecs.RegisterTaskDefinitionAsync(Arg.Any<RegisterTaskDefinitionRequest>(), Arg.Any<CancellationToken>())
           .Returns(new RegisterTaskDefinitionResponse { TaskDefinition = new TaskDefinition { Family = "fam-x", Revision = 7 } });

        ecs.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeServicesResponse
           {
               Services = [new Service
               {
                   ServiceName = "svc-x",
                   TaskDefinition = "fam-x:1",
                   Deployments = [new EcsDeployment
                   {
                       RolloutState = DeploymentRolloutState.FAILED,
                       RolloutStateReason = "task failed to start",
                       UpdatedAt = DateTime.UtcNow
                   }]
               }]
           });

        ecs.UpdateServiceAsync(Arg.Any<UpdateServiceRequest>(), Arg.Any<CancellationToken>())
           .Returns(new UpdateServiceResponse
           {
               Service = new Service
               {
                   ServiceName = "svc-x",
                   TaskDefinition = "fam-x:7",
                   Deployments = [new EcsDeployment
                   {
                       RolloutState = DeploymentRolloutState.FAILED,
                       RolloutStateReason = "task failed to start",
                       UpdatedAt = DateTime.UtcNow
                   }]
               }
           });

        var convention = new UpdateEcsServiceConvention(
            ecs, MakeInputs(WaitOptionType.WaitUntilCompleted), [], new InMemoryLog(),
            deploymentPollInterval: () => TimeSpan.Zero,
            taskPollInterval: () => TimeSpan.Zero);

        var act = async () => await convention.InstallAsync(new RunningDeployment(new CalamariVariables()));
        await act.Should().ThrowAsync<CommandException>().WithMessage("*Reached deployment state: FAILED*");
    }

    [Test]
    public async Task NonEcsDeploymentController_SkipsBothWaits()
    {
        var template = new TaskDefinition
        {
            Family = "fam-x",
            ContainerDefinitions = [new ContainerDefinition { Name = "web", Image = "old:1" }]
        };
        var ecs = CreateEcs();

        ecs.DescribeTaskDefinitionAsync(Arg.Any<DescribeTaskDefinitionRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeTaskDefinitionResponse { TaskDefinition = template });

        ecs.RegisterTaskDefinitionAsync(Arg.Any<RegisterTaskDefinitionRequest>(), Arg.Any<CancellationToken>())
           .Returns(new RegisterTaskDefinitionResponse { TaskDefinition = new TaskDefinition { Family = "fam-x", Revision = 7 } });

        ecs.DescribeServicesAsync(Arg.Any<DescribeServicesRequest>(), Arg.Any<CancellationToken>())
           .Returns(new DescribeServicesResponse
           {
               Services = [new Service
               {
                   ServiceName = "svc-x",
                   TaskDefinition = "fam-x:1",
                   DeploymentController = new DeploymentController { Type = DeploymentControllerType.CODE_DEPLOY }
               }]
           });

        ecs.UpdateServiceAsync(Arg.Any<UpdateServiceRequest>(), Arg.Any<CancellationToken>())
           .Returns(new UpdateServiceResponse
           {
               Service = new Service
               {
                   ServiceName = "svc-x",
                   TaskDefinition = "fam-x:7",
                   DeploymentController = new DeploymentController { Type = DeploymentControllerType.CODE_DEPLOY }
               }
           });

        var convention = new UpdateEcsServiceConvention(ecs, MakeInputs(WaitOptionType.WaitUntilCompleted), [], new InMemoryLog());

        await convention.InstallAsync(new RunningDeployment(new CalamariVariables()));

        await ecs.DidNotReceive().ListTasksAsync(Arg.Any<ListTasksRequest>(), Arg.Any<CancellationToken>());
    }
}
