using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using InvalidOperationException = System.InvalidOperationException;

namespace Calamari.Tests.AWS.CloudFormation;

[TestFixture]
public class WaitForStackToCompleteTests
{
    static readonly StackArn Stack = new("test-stack");

    [Test]
    public async Task ReturnsImmediately_WhenStackDoesNotExist()
    {
        var client = Substitute.For<IAmazonCloudFormation>();
        client.DescribeStacksAsync(Arg.Any<DescribeStacksRequest>())
              .Returns<Task<DescribeStacksResponse>>(_ => throw new AmazonCloudFormationException("Stack does not exist") { ErrorCode = "ValidationError" });

        await Factory(client).WaitForStackToComplete(TimeSpan.FromMilliseconds(50), Stack);

        await client.Received(1).DescribeStacksAsync(Arg.Any<DescribeStacksRequest>());
    }

    [Test]
    public async Task ReturnsImmediately_WhenStackAlreadyCompleted()
    {
        var client = Substitute.For<IAmazonCloudFormation>();
        client.DescribeStacksAsync(Arg.Any<DescribeStacksRequest>())
              .Returns(StackResponse("CREATE_COMPLETE"));

        await Factory(client).WaitForStackToComplete(TimeSpan.FromMilliseconds(50), Stack);

        await client.Received(1).DescribeStacksAsync(Arg.Any<DescribeStacksRequest>());
    }

    [Test]
    public void ThrowsTimeoutException_WhenStackStaysInProgressPastTimeout()
    {
        var client = Substitute.For<IAmazonCloudFormation>();
        client.DescribeStacksAsync(Arg.Any<DescribeStacksRequest>())
              .Returns(StackResponse("CREATE_IN_PROGRESS"));
        client.DescribeStackEventsAsync(Arg.Any<DescribeStackEventsRequest>())
              .Returns(new DescribeStackEventsResponse { StackEvents = [] });

        Assert.ThrowsAsync<TimeoutException>(() =>
                                                 Factory(client)
                                                     .WaitForStackToComplete(
                                                                             pollPeriod: TimeSpan.FromMilliseconds(25),
                                                                             stack: Stack,
                                                                             timeout: TimeSpan.FromMilliseconds(150)));
    }

    [Test]
    public void PropagatesExceptionFromAction_WhenStackEntersRollbackState()
    {
        // Protects the fail-on-rollback contract. DeployAwsCloudFormationConvention
        // passes LogAndThrowRollbacks as the action, which throws on rollback events.
        // If WaitForStackToComplete ever starts swallowing action exceptions, rollback
        // deploys would silently succeed — this is the sole source of failure signal
        // after LogEcsTaskFailuresConvention was dropped.
        var client = Substitute.For<IAmazonCloudFormation>();
        var callCount = 0;
        client.DescribeStacksAsync(Arg.Any<DescribeStacksRequest>())
              .Returns(_ =>
                       {
                           callCount++;
                           // First call reports in-progress so we enter the poll loop where
                           // the action fires; subsequent calls report the rollback terminal
                           // state so the wait would otherwise exit cleanly.
                           return StackResponse(callCount == 1 ? "UPDATE_IN_PROGRESS" : "UPDATE_ROLLBACK_COMPLETE");
                       });
        client.DescribeStackEventsAsync(Arg.Any<DescribeStackEventsRequest>())
              .Returns(new DescribeStackEventsResponse
              {
                  StackEvents = [new StackEvent { ResourceStatus = new ResourceStatus("UPDATE_ROLLBACK_COMPLETE") }]
              });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                                                                   Factory(client)
                                                                       .WaitForStackToComplete(
                                                                                               pollPeriod: TimeSpan.FromMilliseconds(25),
                                                                                               stack: Stack,
                                                                                               action: _ => throw new InvalidOperationException("rollback detected"),
                                                                                               timeout: TimeSpan.FromSeconds(5)));
        ex!.Message.Should().Be("rollback detected");
    }

    [Test]
    public async Task ReturnsWithoutTimeout_WhenStackTransitionsToCompleteWithinWindow()
    {
        var client = Substitute.For<IAmazonCloudFormation>();
        var callCount = 0;
        client.DescribeStacksAsync(Arg.Any<DescribeStacksRequest>())
              .Returns(_ =>
                       {
                           callCount++;
                           // First 2 polls report in-progress, subsequent polls report complete.
                           var status = callCount <= 2 ? "CREATE_IN_PROGRESS" : "CREATE_COMPLETE";
                           return StackResponse(status);
                       });
        client.DescribeStackEventsAsync(Arg.Any<DescribeStackEventsRequest>())
              .Returns(new DescribeStackEventsResponse { StackEvents = [] });

        await Factory(client)
            .WaitForStackToComplete(
                                    pollPeriod: TimeSpan.FromMilliseconds(25),
                                    stack: Stack,
                                    timeout: TimeSpan.FromSeconds(5));

        Assert.That(callCount, Is.GreaterThan(1));
    }

    static Func<IAmazonCloudFormation> Factory(IAmazonCloudFormation client) => () => client;

    static Task<DescribeStacksResponse> StackResponse(string status) =>
        Task.FromResult(new DescribeStacksResponse
        {
            Stacks = [new Stack { StackName = Stack.Value, StackStatus = new StackStatus(status) }]
        });
}
