using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class ArgoCDOutputVariablesWriterTests
    {
        InMemoryLog log;
        ArgoCDOutputVariablesWriter writer;

        const string GatewayName = "TestGateway";
        const string ApplicationName = "TestApp";
        const string ApplicationNamespace = "argocd";
        static readonly QualifiedApplicationName QualifiedApplicationName = QualifiedApplicationName.Create(ApplicationName, ApplicationNamespace);
        static readonly QualifiedApplicationName SecondApplicationName = Calamari.ArgoCD.Models.QualifiedApplicationName.Create("OtherApp", ApplicationNamespace);
        const string CommitSha = "1234567890abcdef1234567890abcdef12345678";
        const string ShortSha = "1234567";
        static readonly DateTimeOffset Timestamp = DateTimeOffset.UtcNow;

        const string RepositoryUrl = "https://github.com/org/repo";
        const string PrTitle = "Update ArgoCD manifests";
        const string PrUrl = "https://github.com/org/repo/pull/123";
        const long PrNumber = 123;

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
            writer = new ArgoCDOutputVariablesWriter(log);
        }

        [Test]
        public void WriteSourceUpdateResultOutputWhenPushResultExists_NoPushResult_NoOutputVariablesAreWritten()
        {
            // Arrange
            const int sourceIndex = 0;
            var sourceUpdateResult = new SourceUpdateResult([], null, [], []);

            // Act
            writer.WriteSourceUpdateResultOutputWhenPushResultExists(GatewayName, QualifiedApplicationName, sourceIndex, sourceUpdateResult);

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            AssertZeroCommitVariables(serviceMessages, sourceIndex);
            AssertNoPullRequestVariables(serviceMessages, sourceIndex);
           
            var pullRequestCreatedServiceMessages = log.Messages.GetServiceMessagesOfType("pull-request-created");
            pullRequestCreatedServiceMessages.Should().BeEmpty();
        }

        [Test]
        public void WriteSourceUpdateResultOutputWhenPushResultExists_WithoutPullRequest_WritesCommitOutputVariables()
        {
            // Arrange
            const int sourceIndex = 0;
            var pullResult = new PushResult(CommitSha, ShortSha, Timestamp);
            var sourceUpdateResult = new SourceUpdateResult([], pullResult, [], []);

            // Act
            writer.WriteSourceUpdateResultOutputWhenPushResultExists(GatewayName, QualifiedApplicationName, sourceIndex, sourceUpdateResult);

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            AssertCommitVariables(serviceMessages, sourceIndex);
            AssertNoPullRequestVariables(serviceMessages, sourceIndex);

            var pullRequestCreatedServiceMessages = log.Messages.GetServiceMessagesOfType("pull-request-created");
            pullRequestCreatedServiceMessages.Should().BeEmpty();
        }

        [Test]
        public void WritePushResultOutput_WithPullRequest_WritesCommitAndPullRequestOutputVariables()
        {
            // Arrange
            const int sourceIndex = 1;
            var pullRequestPushResult = new PullRequestPushResult(CommitSha,
                                                                  ShortSha,
                                                                  Timestamp,
                                                                  RepositoryUrl,
                                                                  PrTitle,
                                                                  PrUrl,
                                                                  PrNumber,
                                                                  "GitLab");
            var sourceUpdateResult = new SourceUpdateResult([], pullRequestPushResult, [], []);

            // Act
            writer.WriteSourceUpdateResultOutputWhenPushResultExists(GatewayName, QualifiedApplicationName, sourceIndex, sourceUpdateResult);

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            AssertCommitVariables(serviceMessages, sourceIndex);
            AssertPullRequestVariables(serviceMessages, sourceIndex);
            
            var pullRequestCreatedServiceMessages = log.Messages.GetServiceMessagesOfType("pull-request-created");
            AssertPullRequestCreatedServiceMessage(pullRequestCreatedServiceMessages, pullRequestPushResult);
        }

        [Test]
        public void WritePushResultOutput_WithMultipleSources_WritesVariablesWithCorrectIndices()
        {
            // Arrange
            const string commitSha2 = "abcdef1234567890abcdef1234567890abcdef12";
            const string shortSha2 = "abcdef1";

            var pushResult1 = new PushResult(CommitSha, ShortSha, Timestamp);
            var sourceUpdateResult1 = new SourceUpdateResult([], pushResult1, [], []);

            var pushResult2 = new PullRequestPushResult(commitSha2,
                                                        shortSha2,
                                                        Timestamp,
                                                        RepositoryUrl,
                                                        PrTitle,
                                                        PrUrl,
                                                        PrNumber,
                                                        "BitBucket");
            var sourceUpdateResult2 = new SourceUpdateResult([], pushResult2, [], []);

            // Act
            writer.WriteSourceUpdateResultOutputWhenPushResultExists(GatewayName, QualifiedApplicationName, 0, sourceUpdateResult1);
            writer.WriteSourceUpdateResultOutputWhenPushResultExists(GatewayName, QualifiedApplicationName, 1, sourceUpdateResult2);

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");
            
            // Source 0
            AssertCommitVariables(serviceMessages, 0);
            AssertNoPullRequestVariables(serviceMessages, 0);

            // Source 1
            AssertCommitVariables(serviceMessages, 1, commitSha2, shortSha2);
            AssertPullRequestVariables(serviceMessages, 1);
            
            var pullRequestCreatedServiceMessages = log.Messages.GetServiceMessagesOfType("pull-request-created");
            AssertPullRequestCreatedServiceMessage(pullRequestCreatedServiceMessages, pushResult2);
        }

        [Test]
        public void WriteManifestUpdateOutput_SingleItems_WritesAllOutputVariables()
        {
            // Arrange
            var appResult = new ProcessApplicationResult(
                gatewayId: "gateway-1",
                gatewayName: "gateway-1",
                applicationName: QualifiedApplicationName,
                totalSourceCount: 3,
                matchingSourceCount: 2,
                trackedSourceDetails:
                [
                    new TrackedSourceDetail("https://github.com/org/repo", "abc123", DateTimeOffset.UtcNow, 0, [], [],[]),
                    new TrackedSourceDetail("https://github.com/org/repo", "def456", DateTimeOffset.UtcNow, 1, [], [],[]),
                ],
                updatedImages: new HashSet<string>(),
                gitReposUpdated: ["https://github.com/org/repo"]);
            var applicationResults = new[] { appResult };
            var totalApps = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApps = applicationResults.Where(r => r.Updated).Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();

            // Act
            writer.WriteManifestUpdateOutput(applicationResults);

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            serviceMessages.GetPropertyValue("ArgoCD.GatewayIds").Should().Be("gateway-1");
            serviceMessages.GetPropertyValue("ArgoCD.GitUris").Should().Be("https://github.com/org/repo");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplications").Should().Be(QualifiedApplicationName.ToString());
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationTotalSourceCounts").Should().Be("3");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationMatchingSourceCounts").Should().Be("2");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplications").Should().Be(QualifiedApplicationName.ToString());
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplicationSourceCounts").Should().Be("2");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedImages").Should().BeNull();
        }

        [Test]
        public void WriteManifestUpdateOutput_MultipleItems_WritesCommaSeparatedValues()
        {
            // Arrange
            var appResult1 = new ProcessApplicationResult(
                gatewayId: "gateway-1",
                gatewayName: "gateway-1",
                applicationName: QualifiedApplicationName,
                totalSourceCount: 3,
                matchingSourceCount: 2,
                trackedSourceDetails:
                [
                    new TrackedSourceDetail("https://github.com/org/repo-a", "abc123", DateTimeOffset.UtcNow, 0, [], [],[]),
                    new TrackedSourceDetail("https://github.com/org/repo-a", "def456", DateTimeOffset.UtcNow, 1, [], [],[]),
                ],
                updatedImages: new HashSet<string>(),
                gitReposUpdated: ["https://github.com/org/repo-a"]);
            var appResult2 = new ProcessApplicationResult(
                gatewayId: "gateway-2",
                gatewayName: "gateway-2",
                applicationName: SecondApplicationName,
                totalSourceCount: 1,
                matchingSourceCount: 1,
                trackedSourceDetails:
                [
                    new TrackedSourceDetail("https://github.com/org/repo-b", "ghi789", DateTimeOffset.UtcNow, 0, [], [],[]),
                ],
                updatedImages: new HashSet<string>(),
                gitReposUpdated: ["https://github.com/org/repo-b"]);
            var applicationResults = new[] { appResult1, appResult2 };
            var totalApps = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApps = applicationResults.Where(r => r.Updated).Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();

            // Act
            writer.WriteManifestUpdateOutput(applicationResults);

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            serviceMessages.GetPropertyValue("ArgoCD.GatewayIds").Should().Be("gateway-1, gateway-2");
            serviceMessages.GetPropertyValue("ArgoCD.GitUris").Should().Be("https://github.com/org/repo-a, https://github.com/org/repo-b");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplications").Should().Be($"{QualifiedApplicationName}, {SecondApplicationName}");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationTotalSourceCounts").Should().Be("3, 1");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationMatchingSourceCounts").Should().Be("2, 1");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplications").Should().Be($"{QualifiedApplicationName}, {SecondApplicationName}");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplicationSourceCounts").Should().Be("2, 1");
        }

        [Test]
        public void WriteManifestUpdateOutput_EmptyCollections_WritesEmptyValues()
        {
            // Arrange
            var applicationResults = Array.Empty<ProcessApplicationResult>();
            var totalApps = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApps = applicationResults.Where(r => r.Updated).Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();

            // Act
            writer.WriteManifestUpdateOutput(applicationResults);

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            serviceMessages.GetPropertyValue("ArgoCD.GatewayIds").Should().BeEmpty();
            serviceMessages.GetPropertyValue("ArgoCD.GitUris").Should().BeEmpty();
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplications").Should().BeEmpty();
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationTotalSourceCounts").Should().BeEmpty();
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationMatchingSourceCounts").Should().BeEmpty();
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplications").Should().BeEmpty();
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplicationSourceCounts").Should().BeEmpty();
        }

        [Test]
        public void WriteImageUpdateOutput_SingleItems_WritesAllOutputVariablesIncludingUpdatedImages()
        {
            // Arrange
            var appResult = new ProcessApplicationResult(
                gatewayId: "gateway-1",
                gatewayName: "gateway-1",
                applicationName: QualifiedApplicationName,
                totalSourceCount: 3,
                matchingSourceCount: 2,
                trackedSourceDetails:
                [
                    new TrackedSourceDetail("https://github.com/org/repo", "abc123", DateTimeOffset.UtcNow, 0, [], [],[]),
                    new TrackedSourceDetail("https://github.com/org/repo", "def456", DateTimeOffset.UtcNow, 1, [], [],[]),
                ],
                updatedImages: ["image-a:1.0", "image-b:2.0", "image-c:3.0", "image-d:4.0", "image-e:5.0"],
                gitReposUpdated: ["https://github.com/org/repo"]);
            var applicationResults = new[] { appResult };
            var totalApps = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApps = applicationResults.Where(r => r.Updated).Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();
            var imagesUpdatedCount = applicationResults.SelectMany(r => r.UpdatedImages).ToHashSet().Count;

            // Act
            writer.WriteImageUpdateOutput(applicationResults.ToList());

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            serviceMessages.GetPropertyValue("ArgoCD.GatewayIds").Should().Be("gateway-1");
            serviceMessages.GetPropertyValue("ArgoCD.GitUris").Should().Be("https://github.com/org/repo");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplications").Should().Be(QualifiedApplicationName.ToString());
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationTotalSourceCounts").Should().Be("3");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationMatchingSourceCounts").Should().Be("2");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplications").Should().Be(QualifiedApplicationName.ToString());
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplicationSourceCounts").Should().Be("2");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedImages").Should().Be("5");
        }

        [Test]
        public void WriteImageUpdateOutput_MultipleItems_WritesCommaSeparatedValues()
        {
            // Arrange
            var appResult1 = new ProcessApplicationResult(
                gatewayId: "gateway-1",
                gatewayName: "gateway-1",
                applicationName: QualifiedApplicationName,
                totalSourceCount: 4,
                matchingSourceCount: 3,
                trackedSourceDetails:
                [
                    new TrackedSourceDetail("https://github.com/org/repo-a", "abc123", DateTimeOffset.UtcNow, 0, [], [],[]),
                    new TrackedSourceDetail("https://github.com/org/repo-a", "def456", DateTimeOffset.UtcNow, 1, [], [],[]),
                    new TrackedSourceDetail("https://github.com/org/repo-a", "ghi789", DateTimeOffset.UtcNow, 2, [], [],[]),
                ],
                updatedImages: ["image-a:1.0", "image-b:2.0"],
                gitReposUpdated: ["https://github.com/org/repo-a"]);
            var appResult2 = new ProcessApplicationResult(
                gatewayId: "gateway-2",
                gatewayName: "gateway-2",
                applicationName: SecondApplicationName,
                totalSourceCount: 2,
                matchingSourceCount: 1,
                trackedSourceDetails:
                [
                    new TrackedSourceDetail("https://github.com/org/repo-b", "jkl012", DateTimeOffset.UtcNow, 0, [], [],[]),
                ],
                updatedImages: ["image-c:3.0", "image-d:4.0"],
                gitReposUpdated: ["https://github.com/org/repo-b"]);
            var applicationResults = new[] { appResult1, appResult2 };
            var totalApps = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApps = applicationResults.Where(r => r.Updated).Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();
            var imagesUpdatedCount = applicationResults.SelectMany(r => r.UpdatedImages).ToHashSet().Count;

            // Act
            writer.WriteImageUpdateOutput(applicationResults.ToList());

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            serviceMessages.GetPropertyValue("ArgoCD.GatewayIds").Should().Be("gateway-1, gateway-2");
            serviceMessages.GetPropertyValue("ArgoCD.GitUris").Should().Be("https://github.com/org/repo-a, https://github.com/org/repo-b");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplications").Should().Be($"{QualifiedApplicationName}, {SecondApplicationName}");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationTotalSourceCounts").Should().Be("4, 2");
            serviceMessages.GetPropertyValue("ArgoCD.MatchingApplicationMatchingSourceCounts").Should().Be("3, 1");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplications").Should().Be($"{QualifiedApplicationName}, {SecondApplicationName}");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedApplicationSourceCounts").Should().Be("3, 1");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedImages").Should().Be("4");
        }

        [Test]
        public void WriteImageUpdateOutput_ZeroImagesUpdated_WritesZeroForUpdatedImages()
        {
            // Arrange
            var applicationResults = Array.Empty<ProcessApplicationResult>();
            var totalApps = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApps = applicationResults.Where(r => r.Updated).Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();
            var imagesUpdatedCount = applicationResults.SelectMany(r => r.UpdatedImages).ToHashSet().Count;

            // Act
            writer.WriteImageUpdateOutput(applicationResults.ToList());

            // Assert
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");
            serviceMessages.GetPropertyValue("ArgoCD.UpdatedImages").Should().Be("0");
        }

        //Zero = No (but NO COMMIT is part of the forbidden words list)
        static void AssertZeroCommitVariables(ServiceMessage[] serviceMessages, int sourceIndex)
        {
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{QualifiedApplicationName}].Source[{sourceIndex}].CommitSha").Should().BeNull();
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{QualifiedApplicationName}].Source[{sourceIndex}].ShortSha").Should().BeNull();
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{QualifiedApplicationName}].Source[{sourceIndex}].CommitTimestamp").Should().BeNull();
        }

        static void AssertCommitVariables(ServiceMessage[] serviceMessages, int sourceIndex, string commitSha = CommitSha, string shortSha = ShortSha)
        {
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{QualifiedApplicationName}].Source[{sourceIndex}].CommitSha").Should().Be(commitSha);
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{QualifiedApplicationName}].Source[{sourceIndex}].ShortSha").Should().Be(shortSha);
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{QualifiedApplicationName}].Source[{sourceIndex}].CommitTimestamp").Should().Be(Timestamp.ToString("o"));
        }

        static void AssertNoPullRequestVariables(ServiceMessage[] serviceMessages, int sourceIndex)
        {
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].PullRequest.Title").Should().BeNull();
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].PullRequest.Number").Should().BeNull();
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].PullRequest.Url").Should().BeNull();
        }

        static void AssertPullRequestVariables(ServiceMessage[] serviceMessages, int sourceIndex)
        {
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{QualifiedApplicationName}].Source[{sourceIndex}].PullRequest.Title").Should().Be(PrTitle);
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{QualifiedApplicationName}].Source[{sourceIndex}].PullRequest.Number").Should().Be(PrNumber.ToString());
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{QualifiedApplicationName}].Source[{sourceIndex}].PullRequest.Url").Should().Be(PrUrl);
        }

        static void AssertPullRequestCreatedServiceMessage(ServiceMessage[] serviceMessages, PullRequestPushResult pushResult)
        {
            var serviceMessage = serviceMessages.Should().ContainSingle().Subject;
            serviceMessage.Name.Should().Be("pull-request-created");

            serviceMessage.GetValue("pullRequestUri").Should().Be(pushResult.PullRequestUri);
            serviceMessage.GetValue("repositoryUri").Should().Be(pushResult.RepositoryUri);
            serviceMessage.GetValue("vendorName").Should().Be(pushResult.VendorName);
            serviceMessage.GetValue("sourceType").Should().Be("ArgoCD");
        }
    }
}