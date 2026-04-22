using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation;
using NSubstitute;
using NUnit.Framework;

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
              .Returns(new DescribeStackEventsResponse { StackEvents = new List<StackEvent>() });

        Assert.ThrowsAsync<TimeoutException>(() =>
                                                 Factory(client)
                                                     .WaitForStackToComplete(
                                                                             pollPeriod: TimeSpan.FromMilliseconds(25),
                                                                             stack: Stack,
                                                                             timeout: TimeSpan.FromMilliseconds(150)));
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
              .Returns(new DescribeStackEventsResponse { StackEvents = new List<StackEvent>() });

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
