using System.Threading;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Aws.Integration.CloudFormation;
using Calamari.Common.Commands;
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
    public class ExecuteCloudFormationChangeSetConventionFixture
    {
        IAmazonCloudFormation client;
        ExecuteCloudFormationChangeSetConvention convention;

        static StackArn TestStack() => new("test-stack");
        static ChangeSetArn TestChangeSet() => new("test-changeset");
        static RunningDeployment TestDeployment() => new(null, new CalamariVariables());

        [SetUp]
        public void SetUp()
        {
            client = Substitute.For<IAmazonCloudFormation>();
            client.DeleteChangeSetAsync(Arg.Any<DeleteChangeSetRequest>(), Arg.Any<CancellationToken>())
                  .Returns(new DeleteChangeSetResponse());

            var log = Substitute.For<ILog>();
            convention = new ExecuteCloudFormationChangeSetConvention(
                () => client,
                new StackEventLogger(log),
                _ => TestStack(),
                _ => TestChangeSet(),
                waitForComplete: false,
                log);
        }

        [Test]
        public void Install_WithNullDescribeChangeSetResponse_DoesNotThrow()
        {
            client.DescribeChangeSetAsync(Arg.Any<DescribeChangeSetRequest>(), Arg.Any<CancellationToken>())
                  .Returns((DescribeChangeSetResponse)null);

            var act = () => convention.Install(TestDeployment());

            act.Should().NotThrow();
        }

        [Test]
        public void Install_WithNullChangesInDescribeChangeSetResponse_DoesNotThrow()
        {
            client.DescribeChangeSetAsync(Arg.Any<DescribeChangeSetRequest>(), Arg.Any<CancellationToken>())
                  .Returns(new DescribeChangeSetResponse { Changes = null });

            var act = () => convention.Install(TestDeployment());

            act.Should().NotThrow();
        }
    }
}
