using System;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
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
            writer.WriteSourceUpdateResultOutputWhenPushResultExists(GatewayName, ApplicationName, sourceIndex, sourceUpdateResult);

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            AssertZeroCommitVariables(serviceMessages, sourceIndex);
            AssertNoPullRequestVariables(serviceMessages, sourceIndex);
        }

        [Test]
        public void WriteSourceUpdateResultOutputWhenPushResultExists_WithoutPullRequest_WritesCommitOutputVariables()
        {
            // Arrange
            const int sourceIndex = 0;
            var pullResult = new PushResult(CommitSha, ShortSha, Timestamp);
            var sourceUpdateResult = new SourceUpdateResult([], pullResult, [], []);

            // Act
            writer.WriteSourceUpdateResultOutputWhenPushResultExists(GatewayName, ApplicationName, sourceIndex, sourceUpdateResult);

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            AssertCommitVariables(serviceMessages, sourceIndex);
            AssertNoPullRequestVariables(serviceMessages, sourceIndex);
        }

        [Test]
        public void WritePushResultOutput_WithPullRequest_WritesCommitAndPullRequestOutputVariables()
        {
            // Arrange
            const int sourceIndex = 1;
            var pullRequestPushResult = new PullRequestPushResult(CommitSha, ShortSha, Timestamp, RepositoryUrl, PrTitle, PrUrl, PrNumber);
            var sourceUpdateResult = new SourceUpdateResult([], pullRequestPushResult, [], []);

            // Act
            writer.WriteSourceUpdateResultOutputWhenPushResultExists(GatewayName, ApplicationName, sourceIndex, sourceUpdateResult);

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            AssertCommitVariables(serviceMessages, sourceIndex);
            AssertPullRequestVariables(serviceMessages, sourceIndex);
        }

        [Test]
        public void WritePushResultOutput_WithMultipleSources_WritesVariablesWithCorrectIndices()
        {
            // Arrange
            const string commitSha2 = "abcdef1234567890abcdef1234567890abcdef12";
            const string shortSha2 = "abcdef1";

            var pushResult1 = new PushResult(CommitSha, ShortSha, Timestamp);
            var sourceUpdateResult1 = new SourceUpdateResult([], pushResult1, [], []);
            
            var pushResult2 = new PullRequestPushResult(commitSha2, shortSha2, Timestamp, RepositoryUrl, PrTitle, PrUrl, PrNumber);
            var sourceUpdateResult2 = new SourceUpdateResult([], pushResult2, [], []);

            // Act
            writer.WriteSourceUpdateResultOutputWhenPushResultExists(GatewayName, ApplicationName, 0, sourceUpdateResult1);
            writer.WriteSourceUpdateResultOutputWhenPushResultExists(GatewayName, ApplicationName, 1, sourceUpdateResult2);

            // Assert
            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            // Source 0
            AssertCommitVariables(serviceMessages, 0);
            AssertNoPullRequestVariables(serviceMessages, 0);

            // Source 1
            AssertCommitVariables(serviceMessages, 1, commitSha2, shortSha2);
            AssertPullRequestVariables(serviceMessages, 1);
        }
        
        //Zero = No (but NOCOMMIT is part of the forbidden words list)
        static void AssertZeroCommitVariables(ServiceMessage[] serviceMessages, int sourceIndex)
        {
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].CommitSha").Should().BeNull();
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].ShortSha").Should().BeNull();
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].CommitTimestamp").Should().BeNull();
        }

        static void AssertCommitVariables(ServiceMessage[] serviceMessages, int sourceIndex, string commitSha = CommitSha, string shortSha = ShortSha)
        {
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].CommitSha").Should().Be(commitSha);
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].ShortSha").Should().Be(shortSha);
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].CommitTimestamp").Should().Be(Timestamp.ToString("o"));
        }

        static void AssertNoPullRequestVariables(ServiceMessage[] serviceMessages, int sourceIndex)
        {
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].PullRequest.Title").Should().BeNull();
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].PullRequest.Number").Should().BeNull();
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].PullRequest.Url").Should().BeNull();
        }

        static void AssertPullRequestVariables(ServiceMessage[] serviceMessages, int sourceIndex)
        {
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].PullRequest.Title").Should().Be(PrTitle);
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].PullRequest.Number").Should().Be(PrNumber.ToString());
            serviceMessages.GetPropertyValue($"ArgoCD.Gateway[{GatewayName}].Application[{ApplicationName}].Source[{sourceIndex}].PullRequest.Url").Should().Be(PrUrl);
        }
    }
}