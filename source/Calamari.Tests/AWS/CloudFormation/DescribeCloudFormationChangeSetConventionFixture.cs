using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation
{
    [TestFixture]
    [Category(TestCategory.PlatformAgnostic)]
    public class DescribeCloudFormationChangeSetConventionFixture
    {
        IAmazonCloudFormation client;
        DescribeCloudFormationChangeSetConvention convention;

        static StackArn TestStack() => new("test-stack");
        static ChangeSetArn TestChangeSet() => new("test-changeset");
        static CalamariVariables TestVariables() => new();

        [SetUp]
        public void SetUp()
        {
            client = Substitute.For<IAmazonCloudFormation>();
            var log = Substitute.For<ILog>();
            convention = new DescribeCloudFormationChangeSetConvention(
                () => client,
                new StackEventLogger(log),
                _ => TestStack(),
                _ => TestChangeSet(),
                log);
        }

        [Test]
        public async Task DescribeChangeset_WithNullChangesInResponseDoesNotThrow()
        {
            client.DescribeChangeSetAsync(Arg.Any<DescribeChangeSetRequest>(), Arg.Any<CancellationToken>())
                  .Returns(new DescribeChangeSetResponse { Changes = null });

            var act = () => convention.DescribeChangeset(TestStack(), TestChangeSet(), TestVariables());
            
            await act.Should().NotThrowAsync();
        }

        [Test]
        public async Task DescribeChangeset_WithNullResponseDoesNotThrow()
        {
            client.DescribeChangeSetAsync(Arg.Any<DescribeChangeSetRequest>(), Arg.Any<CancellationToken>())
                  .Returns((DescribeChangeSetResponse)null);

            var act = () => convention.DescribeChangeset(TestStack(), TestChangeSet(), TestVariables());

            await act.Should().NotThrowAsync();
        }
    }
}
