using System.Collections.Generic;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Models;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class ArgoCDFilesUpdatedReporterTests
    {
        [Test]
        public void ReportDeployments_WithNoUpdatedApplications_WritesNoServiceMessages()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1", new ApplicationName("app1"), 2, 2, [], [], [])
            };

            reporter.ReportFilesUpdated(applicationResults);

            var messages = log.ServiceMessages;
            messages.Should().BeEmpty();
        }

        [Test]
        public void ReportDeployments_WithSingleUpdatedApplication_WritesOneServiceMessage()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1", new ApplicationName("app1"), 2, 2, [new UpdatedSourceDetail("abc123", 0, [], [])], [], [])
            };

            reporter.ReportFilesUpdated(applicationResults);

            log.ServiceMessages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Name = "argocd-files-updated",
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
            var reporter = new ArgoCDFilesUpdatedReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1", new ApplicationName("app1"), 2, 2, [new UpdatedSourceDetail("abc123", 0, [], [])], [], []),
                new("gateway2", new ApplicationName("app2"), 1, 1, [new UpdatedSourceDetail("def456", 0, [], [])], [], [])
            };

            reporter.ReportFilesUpdated(applicationResults);

            log.ServiceMessages.Should().BeEquivalentTo([
                new
                {
                    Name = "argocd-files-updated",
                    Properties = new Dictionary<string, string>
                    {
                        ["gatewayId"] = "gateway1",
                        ["applicationName"] = "app1",
                        ["sources"] = "[{\"CommitSha\":\"abc123\",\"SourceIndex\":0,\"ReplacedFiles\":[],\"PatchedFiles\":[]}]"
                    }
                },
                new
                {
                    Name = "argocd-files-updated",
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
        public void ReportDeployments_WithReplacedFiles_IncludesReplacedFilesInSources()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1", new ApplicationName("app1"), 2, 2,
                    [new UpdatedSourceDetail("abc123", 0, [new FilePathContent("values.yaml", "image: nginx:latest")], [])],
                    [], [])
            };

            reporter.ReportFilesUpdated(applicationResults);

            log.ServiceMessages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Name = "argocd-files-updated",
                Properties = new Dictionary<string, string>
                {
                    ["gatewayId"] = "gateway1",
                    ["applicationName"] = "app1",
                    ["sources"] = "[{\"CommitSha\":\"abc123\",\"SourceIndex\":0,\"ReplacedFiles\":[{\"FilePath\":\"values.yaml\",\"Content\":\"image: nginx:latest\"}],\"PatchedFiles\":[]}]"
                }
            });
        }

        [Test]
        public void ReportDeployments_WithPatchedFiles_IncludesPatchedFilesInSources()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1", new ApplicationName("app1"), 1, 1,
                    [new UpdatedSourceDetail("def456", 0, [], [new FilePathContent("kustomization.yaml", "images:\n- name: nginx")])],
                    [], [])
            };

            reporter.ReportFilesUpdated(applicationResults);

            log.ServiceMessages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Name = "argocd-files-updated",
                Properties = new Dictionary<string, string>
                {
                    ["gatewayId"] = "gateway1",
                    ["applicationName"] = "app1",
                    ["sources"] = "[{\"CommitSha\":\"def456\",\"SourceIndex\":0,\"ReplacedFiles\":[],\"PatchedFiles\":[{\"FilePath\":\"kustomization.yaml\",\"Content\":\"images:\\n- name: nginx\"}]}]"
                }
            });
        }

        [Test]
        public void ReportDeployments_WithBothReplacedAndPatchedFiles_IncludesBothInSources()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1", new ApplicationName("app1"), 2, 2,
                    [new UpdatedSourceDetail("abc123", 0,
                        [new FilePathContent("values.yaml", "image: nginx:latest")],
                        [new FilePathContent("kustomization.yaml", "images:\n- name: nginx")])],
                    [], [])
            };

            reporter.ReportFilesUpdated(applicationResults);

            log.ServiceMessages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Name = "argocd-files-updated",
                Properties = new Dictionary<string, string>
                {
                    ["gatewayId"] = "gateway1",
                    ["applicationName"] = "app1",
                    ["sources"] = "[{\"CommitSha\":\"abc123\",\"SourceIndex\":0,\"ReplacedFiles\":[{\"FilePath\":\"values.yaml\",\"Content\":\"image: nginx:latest\"}],\"PatchedFiles\":[{\"FilePath\":\"kustomization.yaml\",\"Content\":\"images:\\n- name: nginx\"}]}]"
                }
            });
        }

        [Test]
        public void ReportDeployments_WithMultipleReplacedAndPatchedFiles_IncludesAllFilesInSources()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1", new ApplicationName("app1"), 2, 2,
                    [new UpdatedSourceDetail("abc123", 0,
                        [
                            new FilePathContent("values.yaml", "image: nginx:latest"),
                            new FilePathContent("values-prod.yaml", "replicas: 3")
                        ],
                        [
                            new FilePathContent("kustomization.yaml", "images:\n- name: nginx"),
                            new FilePathContent("patch.yaml", "spec:\n  replicas: 3")
                        ])],
                    [], [])
            };

            reporter.ReportFilesUpdated(applicationResults);

            log.ServiceMessages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Name = "argocd-files-updated",
                Properties = new Dictionary<string, string>
                {
                    ["gatewayId"] = "gateway1",
                    ["applicationName"] = "app1",
                    ["sources"] = "[{\"CommitSha\":\"abc123\",\"SourceIndex\":0,\"ReplacedFiles\":[{\"FilePath\":\"values.yaml\",\"Content\":\"image: nginx:latest\"},{\"FilePath\":\"values-prod.yaml\",\"Content\":\"replicas: 3\"}],\"PatchedFiles\":[{\"FilePath\":\"kustomization.yaml\",\"Content\":\"images:\\n- name: nginx\"},{\"FilePath\":\"patch.yaml\",\"Content\":\"spec:\\n  replicas: 3\"}]}]"
                }
            });
        }

        [Test]
        public void ReportDeployments_WithMultipleSourcesWithFiles_IncludesAllSourcesInSources()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1", new ApplicationName("app1"), 2, 2,
                    [
                        new UpdatedSourceDetail("abc123", 0, [new FilePathContent("values.yaml", "image: nginx:latest")], []),
                        new UpdatedSourceDetail("abc123", 1, [], [new FilePathContent("kustomization.yaml", "images:\n- name: redis")])
                    ],
                    [], [])
            };

            reporter.ReportFilesUpdated(applicationResults);

            log.ServiceMessages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Name = "argocd-files-updated",
                Properties = new Dictionary<string, string>
                {
                    ["gatewayId"] = "gateway1",
                    ["applicationName"] = "app1",
                    ["sources"] = "[{\"CommitSha\":\"abc123\",\"SourceIndex\":0,\"ReplacedFiles\":[{\"FilePath\":\"values.yaml\",\"Content\":\"image: nginx:latest\"}],\"PatchedFiles\":[]},{\"CommitSha\":\"abc123\",\"SourceIndex\":1,\"ReplacedFiles\":[],\"PatchedFiles\":[{\"FilePath\":\"kustomization.yaml\",\"Content\":\"images:\\n- name: redis\"}]}]"
                }
            });
        }

        [Test]
        public void ReportDeployments_WithMixedUpdatedAndNonUpdatedApplications_WritesOnlyUpdatedMessages()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1", new ApplicationName("app1"), 2, 2, [], [], []),
                new("gateway2", new ApplicationName("app2"), 1, 1, [new UpdatedSourceDetail("abc123", 0, [], [])], [], []),
                new("gateway3", new ApplicationName("app3"), 1, 1, [], [], [])
            };

            reporter.ReportFilesUpdated(applicationResults);

            log.ServiceMessages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Name = "argocd-files-updated",
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
