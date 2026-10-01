using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation
{
    [TestFixture]
    [Category(TestCategory.PlatformAgnostic)]
    public class CloudFormationObjectExtensionsFixture
    {
        IAmazonCloudFormation client;
        Func<IAmazonCloudFormation> clientFactory;

        static StackArn TestStack() => new("test-stack");

        [SetUp]
        public void SetUp()
        {
            client = Substitute.For<IAmazonCloudFormation>();
            clientFactory = () => client;
        }

        [Test]
        public async Task GetLastStackEvent_WithNullStackEventsInResponse_DoesNotThrow()
        {
            client.DescribeStackEventsAsync(Arg.Any<DescribeStackEventsRequest>(), Arg.Any<CancellationToken>())
                  .Returns(new DescribeStackEventsResponse { StackEvents = null });

            var act = async () => await clientFactory.GetLastStackEvent(TestStack());

            await act.Should().NotThrowAsync();
        }

        [Test]
        public async Task GetStackEvents_WithNullStackEventsInResponse_ReturnsEmptyList()
        {
            client.DescribeStackEventsAsync(Arg.Any<DescribeStackEventsRequest>(), Arg.Any<CancellationToken>())
                  .Returns(new DescribeStackEventsResponse { StackEvents = null, NextToken = null });

            var result = await clientFactory.GetStackEvents(TestStack());

            result.Should().BeEmpty();
        }

        [Test]
        public async Task DescribeStackAsync_WithNullStacksInResponse_ReturnsNull()
        {
            client.DescribeStacksAsync(Arg.Any<DescribeStacksRequest>(), Arg.Any<CancellationToken>())
                  .Returns(new DescribeStacksResponse { Stacks = null });

            var result = await clientFactory.DescribeStackAsync(TestStack());

            result.Should().BeNull();
        }

        [Test]
        public async Task ListStackResourcesAsync_WithNullStackResourceSummaries_ReturnsEmptyList()
        {
            client.ListStackResourcesAsync(Arg.Any<ListStackResourcesRequest>(), Arg.Any<CancellationToken>())
                  .Returns(new ListStackResourcesResponse { StackResourceSummaries = null });

            var result = await clientFactory.ListStackResourcesAsync(TestStack());

            result.Should().BeEmpty();
        }
    }
}
