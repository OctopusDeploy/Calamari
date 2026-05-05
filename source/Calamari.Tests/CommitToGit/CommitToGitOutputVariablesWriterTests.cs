using System;
using Calamari.ArgoCD.Git;
using Calamari.CommitToGit;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Testing.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;

namespace Calamari.Tests.CommitToGit
{
    [TestFixture]
    public class CommitToGitOutputVariablesWriterTests
    {
        const string CommitSha = "1234567890abcdef1234567890abcdef12345678";
        const string ShortSha = "1234567";
        static readonly DateTimeOffset Timestamp = DateTimeOffset.UtcNow;

        const string RepositoryUrl = "https://github.com/org/repo";
        const string PrTitle = "Update manifests";
        const string PrUrl = "https://github.com/org/repo/pull/123";
        const long PrNumber = 123;

        InMemoryLog log;
        CommitToGitOutputVariablesWriter writer;

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
            writer = new CommitToGitOutputVariablesWriter(log);
        }

        [Test]
        public void WritePushResultOutput_WhenPushResultIsNull_NoOutputVariablesAreWritten()
        {
            writer.WritePushResultOutput(null);

            log.Messages.GetServiceMessagesOfType("setVariable").Should().BeEmpty();
        }

        [Test]
        public void WritePushResultOutput_WhenPushResultIsNotPullRequest_WritesCommitVariablesOnly()
        {
            writer.WritePushResultOutput(new PushResult(CommitSha, ShortSha, Timestamp));

            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            AssertCommitVariables(serviceMessages);
            AssertNoPullRequestVariables(serviceMessages);
        }

        [Test]
        public void WritePushResultOutput_WhenPushResultIsPullRequest_WritesCommitAndPullRequestVariables()
        {
            writer.WritePushResultOutput(new PullRequestPushResult(CommitSha, ShortSha, Timestamp, RepositoryUrl, PrTitle, PrUrl, PrNumber));

            using var _ = new AssertionScope();
            var serviceMessages = log.Messages.GetServiceMessagesOfType("setVariable");

            AssertCommitVariables(serviceMessages);
            AssertPullRequestVariables(serviceMessages);
        }

        static void AssertCommitVariables(ServiceMessage[] serviceMessages)
        {
            serviceMessages.GetPropertyValue(CommitToGitOutputVariablesWriter.CommitSha).Should().Be(CommitSha);
            serviceMessages.GetPropertyValue(CommitToGitOutputVariablesWriter.ShortSha).Should().Be(ShortSha);
            serviceMessages.GetPropertyValue(CommitToGitOutputVariablesWriter.CommitTimestamp).Should().Be(Timestamp.ToString("o"));
        }

        static void AssertNoPullRequestVariables(ServiceMessage[] serviceMessages)
        {
            serviceMessages.GetPropertyValue(CommitToGitOutputVariablesWriter.PullRequestTitle).Should().BeNull();
            serviceMessages.GetPropertyValue(CommitToGitOutputVariablesWriter.PullRequestNumber).Should().BeNull();
            serviceMessages.GetPropertyValue(CommitToGitOutputVariablesWriter.PullRequestUrl).Should().BeNull();
            serviceMessages.GetPropertyValue(CommitToGitOutputVariablesWriter.PullRequestRepositoryUrl).Should().BeNull();
        }

        static void AssertPullRequestVariables(ServiceMessage[] serviceMessages)
        {
            serviceMessages.GetPropertyValue(CommitToGitOutputVariablesWriter.PullRequestTitle).Should().Be(PrTitle);
            serviceMessages.GetPropertyValue(CommitToGitOutputVariablesWriter.PullRequestNumber).Should().Be(PrNumber.ToString());
            serviceMessages.GetPropertyValue(CommitToGitOutputVariablesWriter.PullRequestUrl).Should().Be(PrUrl);
            serviceMessages.GetPropertyValue(CommitToGitOutputVariablesWriter.PullRequestRepositoryUrl).Should().Be(RepositoryUrl);
        }
    }
}
