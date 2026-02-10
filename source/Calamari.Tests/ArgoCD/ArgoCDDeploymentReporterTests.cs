using System.Collections.Generic;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Models;
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
                new("gateway1", new ApplicationName("app1"), 2, 2, [], [], [])
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
                new("gateway1", new ApplicationName("app1"), 2, 2, [new UpdatedSourceDetail("abc123", 0, [], [])], [], [])
            };

            reporter.ReportDeployments(applicationResults);

            log.ServiceMessages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Name = "argocd-deployment",
                Properties = new Dictionary<string, string>
                {
                    ["gatewayId"] = "gateway1",
                    ["applicationName"] = "app1",
                    ["sources"] = "[{\"CommitSha\":\"abc123\",\"SourceIndex\":0,\"ReplacedFiles\":[],\"PatchedFiles\":[]}]"
                }
            });
        }

        [Test]
        public void ReportDeployments_WithMultipleUpdatedApplications_WritesMultipleServiceMessages()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDDeploymentReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1", new ApplicationName("app1"), 2, 2, [new UpdatedSourceDetail("abc123", 0, [], [])], [], []),
                new("gateway2", new ApplicationName("app2"), 1, 1, [new UpdatedSourceDetail("def456", 0, [], [])], [], [])
            };

            reporter.ReportDeployments(applicationResults);

            log.ServiceMessages.Should().BeEquivalentTo([
                new
                {
                    Name = "argocd-deployment",
                    Properties = new Dictionary<string, string>
                    {
                        ["gatewayId"] = "gateway1",
                        ["applicationName"] = "app1",
                        ["sources"] = "[{\"CommitSha\":\"abc123\",\"SourceIndex\":0,\"ReplacedFiles\":[],\"PatchedFiles\":[]}]"
                    }
                },
                new
                {
                    Name = "argocd-deployment",
                    Properties = new Dictionary<string, string>
                    {
                        ["gatewayId"] = "gateway2",
                        ["applicationName"] = "app2",
                        ["sources"] = "[{\"CommitSha\":\"def456\",\"SourceIndex\":0,\"ReplacedFiles\":[],\"PatchedFiles\":[]}]"
                    }
                }
            ]);
        }

        [Test]
        public void ReportDeployments_WithMixedUpdatedAndNonUpdatedApplications_WritesOnlyUpdatedMessages()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDDeploymentReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1", new ApplicationName("app1"), 2, 2, [], [], []),
                new("gateway2", new ApplicationName("app2"), 1, 1, [new UpdatedSourceDetail("abc123", 0, [], [])], [], []),
                new("gateway3", new ApplicationName("app3"), 1, 1, [], [], [])
            };

            reporter.ReportDeployments(applicationResults);

            log.ServiceMessages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Name = "argocd-deployment",
                Properties = new Dictionary<string, string>
                {
                    ["gatewayId"] = "gateway2",
                    ["applicationName"] = "app2",
                    ["sources"] = "[{\"CommitSha\":\"abc123\",\"SourceIndex\":0,\"ReplacedFiles\":[],\"PatchedFiles\":[]}]"
                }
            });
        }
    }
}
