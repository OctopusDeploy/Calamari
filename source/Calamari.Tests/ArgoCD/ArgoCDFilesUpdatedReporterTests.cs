using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Calamari.ArgoCD;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Models;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class ArgoCDFilesUpdatedReporterTests
    {
        GitCommitParameters gitParams = new(string.Empty, string.Empty, false);

        [Test]
        public void ReportDeployments_WithNoUpdatedApplications_WritesNoServiceMessages()
        {
            var log = new InMemoryLog();
            var gitParams = new GitCommitParameters(string.Empty, string.Empty, false);
            var reporter = new ArgoCDFilesUpdatedReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1",
                    new ApplicationName("app1"),
                    2,
                    2,
                    [],
                    [],
                    [])
            };

            reporter.ReportFilesUpdated(gitParams, applicationResults);

            var messages = log.ServiceMessages;
            messages.Should().BeEmpty();
        }

        [Test]
        public void ReportDeployments_WithSingleUpdatedApplication_WritesOneServiceMessage()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);
            var timestamp = DateTimeOffset.UtcNow;
            var tsJson = JsonSerializer.Serialize(timestamp).Trim('"');

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1",
                    new ApplicationName("app1"),
                    2,
                    2,
                    [
                        new TrackedSourceDetail("abc123",
                                                timestamp,
                                                0,
                                                [],
                                                [])
                    ],
                    [],
                    [])
            };

            reporter.ReportFilesUpdated(gitParams, applicationResults);

            log.ServiceMessages.Should()
               .ContainSingle()
               .Which.Should()
               .BeEquivalentTo(new
               {
                   Name = "argocd-files-updated",
                   Properties = new Dictionary<string, string>
                   {
                       ["gatewayId"] = "gateway1",
                       ["applicationName"] = "app1",
                       ["sources"] = $"[{{\"CommitSha\":\"abc123\",\"CommitTimestamp\":\"{tsJson}\",\"SourceIndex\":0,\"ReplacedFiles\":[],\"PatchedFiles\":[]}}]"
                   }
               });
        }

        [Test]
        public void ReportDeployments_WithMultipleUpdatedApplications_WritesMultipleServiceMessages()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);
            var timestamp = DateTimeOffset.UtcNow;
            var tsJson = JsonSerializer.Serialize(timestamp).Trim('"');

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1",
                    new ApplicationName("app1"),
                    2,
                    2,
                    [
                        new TrackedSourceDetail("abc123",
                                                timestamp,
                                                0,
                                                [],
                                                [])
                    ],
                    [],
                    []),
                new("gateway2",
                    new ApplicationName("app2"),
                    1,
                    1,
                    [
                        new TrackedSourceDetail("def456",
                                                timestamp,
                                                0,
                                                [],
                                                [])
                    ],
                    [],
                    [])
            };

            reporter.ReportFilesUpdated(gitParams, applicationResults);

            log.ServiceMessages.Should()
               .BeEquivalentTo([
                   new
                   {
                       Name = "argocd-files-updated",
                       Properties = new Dictionary<string, string>
                       {
                           ["gatewayId"] = "gateway1",
                           ["applicationName"] = "app1",
                           ["sources"] = $"[{{\"CommitSha\":\"abc123\",\"CommitTimestamp\":\"{tsJson}\",\"SourceIndex\":0,\"ReplacedFiles\":[],\"PatchedFiles\":[]}}]"
                       }
                   },
                   new
                   {
                       Name = "argocd-files-updated",
                       Properties = new Dictionary<string, string>
                       {
                           ["gatewayId"] = "gateway2",
                           ["applicationName"] = "app2",
                           ["sources"] = $"[{{\"CommitSha\":\"def456\",\"CommitTimestamp\":\"{tsJson}\",\"SourceIndex\":0,\"ReplacedFiles\":[],\"PatchedFiles\":[]}}]"
                       }
                   }
               ]);
        }

        [Test]
        public void ReportDeployments_WithReplacedFiles_IncludesReplacedFilesInSources()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);
            var timestamp = DateTimeOffset.UtcNow;
            var tsJson = JsonSerializer.Serialize(timestamp).Trim('"');

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1",
                    new ApplicationName("app1"),
                    2,
                    2,
                    [
                        new TrackedSourceDetail("abc123",
                                                timestamp,
                                                0,
                                                [new FileHash("values.yaml", "22c0df2cceca5273e4dc569dda52805d27df3360")],
                                                [])
                    ],
                    [],
                    [])
            };

            reporter.ReportFilesUpdated(gitParams, applicationResults);

            log.ServiceMessages.Should()
               .ContainSingle()
               .Which.Should()
               .BeEquivalentTo(new
               {
                   Name = "argocd-files-updated",
                   Properties = new Dictionary<string, string>
                   {
                       ["gatewayId"] = "gateway1",
                       ["applicationName"] = "app1",
                       ["sources"] = $"[{{\"CommitSha\":\"abc123\",\"CommitTimestamp\":\"{tsJson}\",\"SourceIndex\":0,\"ReplacedFiles\":[{{\"FilePath\":\"values.yaml\",\"Hash\":\"22c0df2cceca5273e4dc569dda52805d27df3360\"}}],\"PatchedFiles\":[]}}]"
                   }
               });
        }

        [Test]
        public void ReportDeployments_WithPatchedFiles_IncludesPatchedFilesInSources()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);
            var timestamp = DateTimeOffset.UtcNow;
            var tsJson = JsonSerializer.Serialize(timestamp).Trim('"');

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1",
                    new ApplicationName("app1"),
                    1,
                    1,
                    [
                        new TrackedSourceDetail("def456",
                                                timestamp,
                                                0,
                                                [],
                                                [new FileJsonPatch("kustomization.yaml", """[{"op":"replace","path":"/images/0/name","value":"nginx:latest"}]""")])
                    ],
                    [],
                    [])
            };

            reporter.ReportFilesUpdated(gitParams, applicationResults);

            log.ServiceMessages.Should()
               .ContainSingle()
               .Which.Should()
               .BeEquivalentTo(new
               {
                   Name = "argocd-files-updated",
                   Properties = new Dictionary<string, string>
                   {
                       ["gatewayId"] = "gateway1",
                       ["applicationName"] = "app1",
                       ["sources"] = $"[{{\"CommitSha\":\"def456\",\"CommitTimestamp\":\"{tsJson}\",\"SourceIndex\":0,\"ReplacedFiles\":[],\"PatchedFiles\":[{{\"FilePath\":\"kustomization.yaml\",\"JsonPatch\":\"[{{\\u0022op\\u0022:\\u0022replace\\u0022,\\u0022path\\u0022:\\u0022/images/0/name\\u0022,\\u0022value\\u0022:\\u0022nginx:latest\\u0022}}]\"}}]}}]"
                   }
               });
        }

        [Test]
        public void ReportDeployments_WithBothReplacedAndPatchedFiles_IncludesBothInSources()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);
            var timestamp = DateTimeOffset.UtcNow;
            var tsJson = JsonSerializer.Serialize(timestamp).Trim('"');

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1",
                    new ApplicationName("app1"),
                    2,
                    2,
                    [
                        new TrackedSourceDetail("abc123",
                                                timestamp,
                                                0,
                                                [new FileHash("values.yaml", "22c0df2cceca5273e4dc569dda52805d27df3360")],
                                                [new FileJsonPatch("kustomization.yaml", """[{"op":"replace","path":"/images/0/name","value":"nginx:latest"}]""")])
                    ],
                    [],
                    [])
            };

            reporter.ReportFilesUpdated(gitParams, applicationResults);

            log.ServiceMessages.Should()
               .ContainSingle()
               .Which.Should()
               .BeEquivalentTo(new
               {
                   Name = "argocd-files-updated",
                   Properties = new Dictionary<string, string>
                   {
                       ["gatewayId"] = "gateway1",
                       ["applicationName"] = "app1",
                       ["sources"] = $"[{{\"CommitSha\":\"abc123\",\"CommitTimestamp\":\"{tsJson}\",\"SourceIndex\":0,\"ReplacedFiles\":[{{\"FilePath\":\"values.yaml\",\"Hash\":\"22c0df2cceca5273e4dc569dda52805d27df3360\"}}],\"PatchedFiles\":[{{\"FilePath\":\"kustomization.yaml\",\"JsonPatch\":\"[{{\\u0022op\\u0022:\\u0022replace\\u0022,\\u0022path\\u0022:\\u0022/images/0/name\\u0022,\\u0022value\\u0022:\\u0022nginx:latest\\u0022}}]\"}}]}}]"
                   }
               });
        }

        [Test]
        public void ReportDeployments_WithMultipleReplacedAndPatchedFiles_IncludesAllFilesInSources()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);
            var timestamp = DateTimeOffset.UtcNow;
            var tsJson = JsonSerializer.Serialize(timestamp).Trim('"');

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1",
                    new ApplicationName("app1"),
                    2,
                    2,
                    [
                        new TrackedSourceDetail("abc123",
                                                timestamp,
                                                0,
                                                [
                                                    new FileHash("values.yaml", "22c0df2cceca5273e4dc569dda52805d27df3360"),
                                                    new FileHash("values-prod.yaml", "a3b4c5d6e7f8a3b4c5d6e7f8a3b4c5d6e7f8a3b4")
                                                ],
                                                [
                                                    new FileJsonPatch("kustomization.yaml", """[{"op":"replace","path":"/images/0/name","value":"nginx:latest"}]"""),
                                                    new FileJsonPatch("patch.yaml", """[{"op":"replace","path":"/spec/replicas","value":3}]""")
                                                ])
                    ],
                    [],
                    [])
            };

            reporter.ReportFilesUpdated(gitParams, applicationResults);

            log.ServiceMessages.Should()
               .ContainSingle()
               .Which.Should()
               .BeEquivalentTo(new
               {
                   Name = "argocd-files-updated",
                   Properties = new Dictionary<string, string>
                   {
                       ["gatewayId"] = "gateway1",
                       ["applicationName"] = "app1",
                       ["sources"] = $"[{{\"CommitSha\":\"abc123\",\"CommitTimestamp\":\"{tsJson}\",\"SourceIndex\":0,\"ReplacedFiles\":[{{\"FilePath\":\"values.yaml\",\"Hash\":\"22c0df2cceca5273e4dc569dda52805d27df3360\"}},{{\"FilePath\":\"values-prod.yaml\",\"Hash\":\"a3b4c5d6e7f8a3b4c5d6e7f8a3b4c5d6e7f8a3b4\"}}],\"PatchedFiles\":[{{\"FilePath\":\"kustomization.yaml\",\"JsonPatch\":\"[{{\\u0022op\\u0022:\\u0022replace\\u0022,\\u0022path\\u0022:\\u0022/images/0/name\\u0022,\\u0022value\\u0022:\\u0022nginx:latest\\u0022}}]\"}},{{\"FilePath\":\"patch.yaml\",\"JsonPatch\":\"[{{\\u0022op\\u0022:\\u0022replace\\u0022,\\u0022path\\u0022:\\u0022/spec/replicas\\u0022,\\u0022value\\u0022:3}}]\"}}]}}]"
                   }
               });
        }

        [Test]
        public void ReportDeployments_WithMultipleSourcesWithFiles_IncludesAllSourcesInSources()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);
            var timestamp = DateTimeOffset.UtcNow;
            var tsJson = JsonSerializer.Serialize(timestamp).Trim('"');

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1",
                    new ApplicationName("app1"),
                    2,
                    2,
                    [
                        new TrackedSourceDetail("abc123",
                                                timestamp,
                                                0,
                                                [new FileHash("values.yaml", "22c0df2cceca5273e4dc569dda52805d27df3360")],
                                                []),
                        new TrackedSourceDetail("abc123",
                                                timestamp,
                                                1,
                                                [],
                                                [new FileJsonPatch("kustomization.yaml", """[{"op":"replace","path":"/images/0/name","value":"redis:latest"}]""")])
                    ],
                    [],
                    [])
            };

            reporter.ReportFilesUpdated(gitParams, applicationResults);

            log.ServiceMessages.Should()
               .ContainSingle()
               .Which.Should()
               .BeEquivalentTo(new
               {
                   Name = "argocd-files-updated",
                   Properties = new Dictionary<string, string>
                   {
                       ["gatewayId"] = "gateway1",
                       ["applicationName"] = "app1",
                       ["sources"] = $"[{{\"CommitSha\":\"abc123\",\"CommitTimestamp\":\"{tsJson}\",\"SourceIndex\":0,\"ReplacedFiles\":[{{\"FilePath\":\"values.yaml\",\"Hash\":\"22c0df2cceca5273e4dc569dda52805d27df3360\"}}],\"PatchedFiles\":[]}},{{\"CommitSha\":\"abc123\",\"CommitTimestamp\":\"{tsJson}\",\"SourceIndex\":1,\"ReplacedFiles\":[],\"PatchedFiles\":[{{\"FilePath\":\"kustomization.yaml\",\"JsonPatch\":\"[{{\\u0022op\\u0022:\\u0022replace\\u0022,\\u0022path\\u0022:\\u0022/images/0/name\\u0022,\\u0022value\\u0022:\\u0022redis:latest\\u0022}}]\"}}]}}]"
                   }
               });
        }

        [Test]
        public void ReportDeployments_WithOsSpecificReplacedFilePaths_ReportsPosixPaths()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);
            var timestamp = DateTimeOffset.MinValue;
            var tsJson = JsonSerializer.Serialize(timestamp).Trim('"');

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1",
                    new ApplicationName("app1"),
                    1,
                    1,
                    [
                        new TrackedSourceDetail("abc123",
                                                timestamp,
                                                0,
                                                [new FileHash(Path.Combine("some", "nested", "values.yaml"), "22c0df2cceca5273e4dc569dda52805d27df3360")],
                                                [])
                    ],
                    [],
                    [])
            };

            reporter.ReportFilesUpdated(gitParams, applicationResults);

            log.ServiceMessages.Should()
               .ContainSingle()
               .Which.Should()
               .BeEquivalentTo(new
               {
                   Name = "argocd-files-updated",
                   Properties = new Dictionary<string, string>
                   {
                       ["gatewayId"] = "gateway1",
                       ["applicationName"] = "app1",
                       ["sources"] = $"[{{\"CommitSha\":\"abc123\",\"CommitTimestamp\":\"{tsJson}\",\"SourceIndex\":0,\"ReplacedFiles\":[{{\"FilePath\":\"some/nested/values.yaml\",\"Hash\":\"22c0df2cceca5273e4dc569dda52805d27df3360\"}}],\"PatchedFiles\":[]}}]"
                   }
               });
        }

        [Test]
        public void ReportDeployments_WithNoOpSource_EmitsServiceMessageWithNullCommitSha()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1",
                    new ApplicationName("app1"),
                    1,
                    1,
                    [
                        new TrackedSourceDetail(null,
                                                null,
                                                0,
                                                [],
                                                [new FileJsonPatch("values.yaml", """[{"op":"replace","path":"/image","value":"nginx:1.27"}]""")])
                    ],
                    [],
                    [])
            };

            reporter.ReportFilesUpdated(gitParams, applicationResults);

            log.ServiceMessages.Should()
               .ContainSingle()
               .Which.Should()
               .BeEquivalentTo(new
               {
                   Name = "argocd-files-updated",
                   Properties = new Dictionary<string, string>
                   {
                       ["gatewayId"] = "gateway1",
                       ["applicationName"] = "app1",
                       ["sources"] = """[{"CommitSha":null,"CommitTimestamp":null,"SourceIndex":0,"ReplacedFiles":[],"PatchedFiles":[{"FilePath":"values.yaml","JsonPatch":"[{\u0022op\u0022:\u0022replace\u0022,\u0022path\u0022:\u0022/image\u0022,\u0022value\u0022:\u0022nginx:1.27\u0022}]"}]}]"""
                   }
               });
        }

        [Test]
        public void ReportDeployments_WithMixedUpdatedAndNonUpdatedApplications_WritesOnlyUpdatedMessages()
        {
            var log = new InMemoryLog();
            var reporter = new ArgoCDFilesUpdatedReporter(log);
            var timestamp = DateTimeOffset.UtcNow;
            var tsJson = JsonSerializer.Serialize(timestamp).Trim('"');

            var applicationResults = new List<ProcessApplicationResult>
            {
                new("gateway1",
                    new ApplicationName("app1"),
                    2,
                    2,
                    [],
                    [],
                    []),
                new("gateway2",
                    new ApplicationName("app2"),
                    1,
                    1,
                    [
                        new TrackedSourceDetail("abc123",
                                                timestamp,
                                                0,
                                                [],
                                                [])
                    ],
                    [],
                    []),
                new("gateway3",
                    new ApplicationName("app3"),
                    1,
                    1,
                    [],
                    [],
                    [])
            };

            reporter.ReportFilesUpdated(gitParams, applicationResults);

            log.ServiceMessages.Should()
               .ContainSingle()
               .Which.Should()
               .BeEquivalentTo(new
               {
                   Name = "argocd-files-updated",
                   Properties = new Dictionary<string, string>
                   {
                       ["gatewayId"] = "gateway2",
                       ["applicationName"] = "app2",
                       ["sources"] = $"[{{\"CommitSha\":\"abc123\",\"CommitTimestamp\":\"{tsJson}\",\"SourceIndex\":0,\"ReplacedFiles\":[],\"PatchedFiles\":[]}}]"
                   }
               });
        }
    }
}