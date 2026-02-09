using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class ArgoCDDeploymentReporterTests
    {
        [Test]
        public void ReportDeployments_WithNoUpdatedApplications_WritesNoServiceMessages()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDDeploymentReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new ProcessApplicationResult("gateway1", new ApplicationName("app1"))
                {
                    TotalSourceCount = 2,
                    MatchingSourceCount = 2
                }
            };

            reporter.ReportDeployments(applicationResults);

            var messages = log.ServiceMessages;
            messages.Should().BeEmpty();
        }

        [Test]
        public void ReportDeployments_WithSingleUpdatedApplication_WritesOneServiceMessage()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDDeploymentReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new ProcessApplicationResult("gateway1", new ApplicationName("app1"))
                {
                    TotalSourceCount = 2,
                    MatchingSourceCount = 2,
                    UpdatedSourceDetails =
                    {
                        new UpdatedSourceDetail("abc123", 0, [], [])
                    }
                }
            };

            reporter.ReportDeployments(applicationResults);

            var messages = log.ServiceMessages;
            messages.Should().HaveCount(1);

            var message = messages.First();
            message.Name.Should().Be("argocd-deployment");
            message.Properties["gatewayId"].Should().Be("gateway1");
            message.Properties["applicationName"].Should().Be("app1");
        }

        [Test]
        public void ReportDeployments_WithMultipleUpdatedApplications_WritesMultipleServiceMessages()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDDeploymentReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new ProcessApplicationResult("gateway1", new ApplicationName("app1"))
                {
                    TotalSourceCount = 2,
                    MatchingSourceCount = 2,
                    UpdatedSourceDetails =
                    {
                        new UpdatedSourceDetail("abc123", 0, [], [])
                    }
                },
                new ProcessApplicationResult("gateway2", new ApplicationName("app2"))
                {
                    TotalSourceCount = 1,
                    MatchingSourceCount = 1,
                    UpdatedSourceDetails =
                    {
                        new UpdatedSourceDetail("def456", 0, [], [])
                    }
                }
            };

            reporter.ReportDeployments(applicationResults);

            var messages = log.ServiceMessages;
            messages.Should().HaveCount(2);

            messages[0].Properties["gatewayId"].Should().Be("gateway1");
            messages[0].Properties["applicationName"].Should().Be("app1");

            messages[1].Properties["gatewayId"].Should().Be("gateway2");
            messages[1].Properties["applicationName"].Should().Be("app2");
        }

        [Test]
        public void ReportDeployments_SerializesSourcesCorrectly()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDDeploymentReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new ProcessApplicationResult("gateway1", new ApplicationName("app1"))
                {
                    TotalSourceCount = 2,
                    MatchingSourceCount = 2,
                    UpdatedSourceDetails =
                    {
                        new UpdatedSourceDetail("abc123", 0, [], []),
                        new UpdatedSourceDetail("def456", 1, [], [])
                    }
                }
            };

            reporter.ReportDeployments(applicationResults);

            var messages = log.ServiceMessages;
            messages.Should().HaveCount(1);

            var sourcesJson = messages.First().Properties["sources"];
            var sources = JsonSerializer.Deserialize<List<UpdatedSourceDetail>>(sourcesJson);

            sources.Should().HaveCount(2);
            sources[0].CommitSha.Should().Be("abc123");
            sources[0].SourceIndex.Should().Be(0);
            sources[1].CommitSha.Should().Be("def456");
            sources[1].SourceIndex.Should().Be(1);
        }

        [Test]
        public void ReportDeployments_WithMixedUpdatedAndNonUpdatedApplications_WritesOnlyUpdatedMessages()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDDeploymentReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new ProcessApplicationResult("gateway1", new ApplicationName("app1"))
                {
                    TotalSourceCount = 2,
                    MatchingSourceCount = 2
                },
                new ProcessApplicationResult("gateway2", new ApplicationName("app2"))
                {
                    TotalSourceCount = 1,
                    MatchingSourceCount = 1,
                    UpdatedSourceDetails =
                    {
                        new UpdatedSourceDetail("abc123", 0, [], [])
                    }
                },
                new ProcessApplicationResult("gateway3", new ApplicationName("app3"))
                {
                    TotalSourceCount = 1,
                    MatchingSourceCount = 1
                }
            };

            reporter.ReportDeployments(applicationResults);

            var messages = log.ServiceMessages;
            messages.Should().HaveCount(1);
            messages[0].Properties["gatewayId"].Should().Be("gateway2");
            messages[0].Properties["applicationName"].Should().Be("app2");
        }
    }
}
