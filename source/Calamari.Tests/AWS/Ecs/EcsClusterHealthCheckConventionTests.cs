using System;
using System.Collections.Generic;
using System.Threading;
using Amazon.ECS;
using Amazon.ECS.Model;
using Calamari.Aws.Deployment.Conventions;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.Ecs;

[TestFixture]
public class EcsClusterHealthCheckConventionTests
{
    [TestCase("")]
    [TestCase("    ")]
    public void Install_WithEmptyClusterName_Throws(string clusterName)
    {
        // Arrange
        var fakeClient = Substitute.For<IAmazonECS>();
        var sut = new EcsClusterHealthCheckConvention(clusterName, ClientFactory, Substitute.For<ILog>());

        // Assert
        Assert.Throws<CommandException>(() => sut.Install(new RunningDeployment(new CalamariVariables())));
        return;

        IAmazonECS ClientFactory() => fakeClient;
    }

    [Test]
    public void Install_WithNoActiveClusters_Throws()
    {
        // Arrange
        const string clusterName = "InactiveCluster";
        var fakeClient = Substitute.For<IAmazonECS>();
        var inactiveClusterResponse = new DescribeClustersResponse
        {
            Clusters =
            [
                new Cluster
                {
                    ClusterName = clusterName,
                    Status = "INACTIVE"

                }
            ]
        };
        fakeClient.DescribeClustersAsync(Arg.Is<DescribeClustersRequest>(r => r.Clusters.Contains(clusterName)), Arg.Any<CancellationToken>()).Returns(inactiveClusterResponse);
        var sut = new EcsClusterHealthCheckConvention(clusterName, ClientFactory, Substitute.For<ILog>());

        // Assert
        Assert.Throws<ClusterNotFoundException>(() => sut.Install(new RunningDeployment(new CalamariVariables())));
        return;

        IAmazonECS ClientFactory() => fakeClient;
    }


    [TestCase("ACTIVE")]
    [TestCase("PROVISIONING")]
    [TestCase("DEPROVISIONING")]
    [TestCase("FAILED")]
    public void Install_WithValidCluster_ExecutesSuccessfully(string clusterStatus)
    {
        // Arrange
        const string clusterName = "ValidCluster";
        var fakeClient = Substitute.For<IAmazonECS>();
        var clusterResponse = new DescribeClustersResponse
        {
            Clusters =
            [
                new Cluster
                {
                    ClusterName = clusterName,
                    Status = clusterStatus
                }
            ]
        };
        fakeClient.DescribeClustersAsync(Arg.Is<DescribeClustersRequest>(r => r.Clusters.Contains(clusterName)), Arg.Any<CancellationToken>()).Returns(clusterResponse);
        var sut = new EcsClusterHealthCheckConvention(clusterName, ClientFactory, Substitute.For<ILog>());

        // Assert
        Assert.DoesNotThrow(() => sut.Install(new RunningDeployment(new CalamariVariables())));
        return;

        IAmazonECS ClientFactory() => fakeClient;
    }

    [Test]
    public void Install_WithNoMatchingClusters_Throws()
    {
        // Arrange
        const string clusterName = "ValidCluster";
        var fakeClient = Substitute.For<IAmazonECS>();
        var clusterResponse = new DescribeClustersResponse
        {
            Clusters = []
        };

        fakeClient.DescribeClustersAsync(Arg.Is<DescribeClustersRequest>(r => r.Clusters.Contains(clusterName)), Arg.Any<CancellationToken>()).Returns(clusterResponse);
        var sut = new EcsClusterHealthCheckConvention(clusterName, ClientFactory, Substitute.For<ILog>());

        // Assert
        Assert.Throws<ClusterNotFoundException>(() => sut.Install(new RunningDeployment(new CalamariVariables())));
        return;

        IAmazonECS ClientFactory() => fakeClient;
    }

    [Test]
    public void Install_WithNullClustersInResponse_Throws()
    {
        // Arrange
        const string clusterName = "ValidCluster";
        var fakeClient = Substitute.For<IAmazonECS>();
        var clusterResponse = new DescribeClustersResponse();

        fakeClient.DescribeClustersAsync(Arg.Is<DescribeClustersRequest>(r => r.Clusters.Contains(clusterName)), Arg.Any<CancellationToken>()).Returns(clusterResponse);
        var sut = new EcsClusterHealthCheckConvention(clusterName, ClientFactory, Substitute.For<ILog>());

        // Assert
        Assert.Throws<ClusterNotFoundException>(() => sut.Install(new RunningDeployment(new CalamariVariables())));
        return;

        IAmazonECS ClientFactory() => fakeClient;
    }

    [TestCaseSource(nameof(EcsClientExceptions))]
    public void Install_WhenEcsClientThrows_RethrowsOriginalException(Exception thrown)
    {
        // Arrange
        const string clusterName = "ValidCluster";
        var fakeClient = Substitute.For<IAmazonECS>();
        fakeClient.DescribeClustersAsync(Arg.Any<DescribeClustersRequest>(), Arg.Any<CancellationToken>()).Throws(thrown);
        var sut = new EcsClusterHealthCheckConvention(clusterName, () => fakeClient, Substitute.For<ILog>());

        // Assert
        var actual = Assert.Catch(() => sut.Install(new RunningDeployment(new CalamariVariables())));
        Assert.That(actual, Is.SameAs(thrown));
    }
    
    [TestCaseSource(nameof(FoundClusterCountCases))]
    public void Install_LogsFoundClusterCount(List<Cluster> clusters, int expectedCount)
    {
        // Arrange
        var fakeLog = Substitute.For<ILog>();
        const string clusterName = "ValidCluster";
        var fakeClient = Substitute.For<IAmazonECS>();
        var clusterResponse = clusters is null
            ? new DescribeClustersResponse()
            : new DescribeClustersResponse { Clusters = clusters };

        fakeClient.DescribeClustersAsync(Arg.Is<DescribeClustersRequest>(r => r.Clusters.Contains(clusterName)), Arg.Any<CancellationToken>()).Returns(clusterResponse);
        var sut = new EcsClusterHealthCheckConvention(clusterName, () => fakeClient, fakeLog);

        // Act
        try
        {
            sut.Install(new RunningDeployment(new CalamariVariables()));
        }
        catch
        {
            // Swallow exception for test
        }
        
        // Assert
        fakeLog.Received().Verbose($"Found {expectedCount} cluster(s)");
    }
    
    static readonly Exception[] EcsClientExceptions =
    [
        new ClientException("test"),
        new AccessDeniedException("test"),
        new InvalidParameterException("test"),
        new ServerException("test")
    ];
    
    static IEnumerable<TestCaseData> FoundClusterCountCases()
    {
        yield return new TestCaseData(new List<Cluster> { new() { ClusterName = "ValidCluster", Status = "ACTIVE" } }, 1)
            .SetName("Single matching cluster");
        yield return new TestCaseData(new List<Cluster>
        {
            new() { ClusterName = "ValidCluster", Status = "ACTIVE" },
            new() { ClusterName = "ValidCluster", Status = "INACTIVE" }
        }, 2).SetName("Multiple matching clusters");
        yield return new TestCaseData(new List<Cluster>(), 0).SetName("Empty cluster list");
        yield return new TestCaseData(null, 0).SetName("Null cluster list");
    }
}