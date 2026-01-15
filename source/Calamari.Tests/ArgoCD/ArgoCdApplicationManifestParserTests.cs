using System;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD
{
    [TestFixture]
    public class ArgoCdApplicationManifestParserTests
    {
        [Test]
        public void SingleSource_SourceDeserialized()
        {
            const string appJson = @"{
                                        ""metadata"": {
                                          ""name"": ""foo"",
                                          ""namespace"": ""argocd"",
                                          ""uid"": ""b219be20-4fd2-40d1-ad81-9f5586dfcc04"",
                                          ""annotations"": {
                                            ""argo.octopus.com/environment"": ""<<ENVIRONMENT_SLUG_TOKEN>>"",
                                            ""argo.octopus.com/project"": ""<<PROJECT_SLUG_TOKEN>>""
                                          }
                                        },
                                        ""spec"": {
                                          ""source"": {
                                            ""repoURL"": ""https://github.com/fake/octopus-test.git"",
                                            ""path"": ""octopus-app"",
                                            ""targetRevision"": ""test-branch""
                                          },
                                          ""destination"": {
                                            ""server"": ""https://kubernetes.default.svc"",
                                            ""namespace"": ""octopus-managed-app""
                                          },
                                          ""project"": ""default""
                                        },
                                        ""status"": {
                                          ""sync"": {
                                            ""status"": ""Synced""
                                          },
                                          ""health"": {
                                            ""status"": ""Healthy""
                                          },
                                          ""sourceType"": ""Kustomize"",
                                          ""summary"": {}
                                        }
                                    }";

            var application = new ArgoCdApplicationManifestParser().ParseManifest(appJson);

            application.Spec.Sources.Should()
                       .BeEquivalentTo(new[]
                       {
                           new ApplicationSource()
                           {
                               RepoUrl = new Uri("https://github.com/fake/octopus-test.git"),
                               TargetRevision = "test-branch",
                               Path = "octopus-app"
                           }
                       });
            application.Status.SourceTypes.Should().BeEquivalentTo("Kustomize");
        }

        [Test]
        public void TwoSources_SourcesDeserialized()
        {
            const string appJson = @"{
                                        ""metadata"": {
                                          ""name"": ""foo"",
                                          ""namespace"": ""argocd"",
                                          ""uid"": ""b219be20-4fd2-40d1-ad81-9f5586dfcc04"",
                                          ""annotations"": {
                                            ""argo.octopus.com/environment"": ""<<ENVIRONMENT_SLUG_TOKEN>>"",
                                            ""argo.octopus.com/project"": ""<<PROJECT_SLUG_TOKEN>>""
                                          }
                                        },
                                        ""spec"": {
                                          ""sources"": [{
                                            ""repoURL"": ""https://github.com/fake/octopus-test.git"",
                                            ""path"": ""octopus-app"",
                                            ""targetRevision"": ""test-branch""
                                          },{
                                            ""repoURL"": ""https://github.com/fake/octopus-test2.git"",
                                            ""path"": ""octopus-app2"",
                                            ""targetRevision"": ""test-branch2""
                                          }],
                                          ""destination"": {
                                            ""server"": ""https://kubernetes.default.svc"",
                                            ""namespace"": ""octopus-managed-app""
                                          },
                                          ""project"": ""default""
                                        },
                                        ""status"": {
                                          ""sync"": {
                                            ""status"": ""Synced""
                                          },
                                          ""health"": {
                                            ""status"": ""Healthy""
                                          },
                                          ""sourceTypes"": [""Kustomize"", ""Helm""],
                                          ""summary"": {}
                                        }
                                    }";

            var application = new ArgoCdApplicationManifestParser().ParseManifest(appJson);

            application.Spec.Sources.Should()
                       .BeEquivalentTo(new[]
                       {
                           new ApplicationSource()
                           {
                               RepoUrl = new Uri("https://github.com/fake/octopus-test.git"),
                               TargetRevision = "test-branch",
                               Path = "octopus-app"
                           },
                           new ApplicationSource()
                           {
                               RepoUrl = new Uri("https://github.com/fake/octopus-test2.git"),
                               TargetRevision = "test-branch2",
                               Path = "octopus-app2"
                           }
                       });
            application.Status.SourceTypes.Should().BeEquivalentTo("Kustomize", "Helm");
        }

        [Test]
        public void NoSourceType_SourceTypesIsEmpty()
        {
            const string appJson = @"{
                                        ""metadata"": {
                                          ""name"": ""foo"",
                                          ""namespace"": ""argocd"",
                                          ""uid"": ""b219be20-4fd2-40d1-ad81-9f5586dfcc04"",
                                          ""annotations"": {
                                            ""argo.octopus.com/environment"": ""<<ENVIRONMENT_SLUG_TOKEN>>"",
                                            ""argo.octopus.com/project"": ""<<PROJECT_SLUG_TOKEN>>""
                                          }
                                        },
                                        ""spec"": {
                                          ""source"": {
                                            ""repoURL"": ""https://github.com/fake/octopus-test.git"",
                                            ""path"": ""octopus-app"",
                                            ""targetRevision"": ""test-branch""
                                          },
                                          ""destination"": {
                                            ""server"": ""https://kubernetes.default.svc"",
                                            ""namespace"": ""octopus-managed-app""
                                          },
                                          ""project"": ""default""
                                        },
                                        ""status"": {
                                          ""sync"": {
                                            ""status"": ""Synced""
                                          },
                                          ""health"": {
                                            ""status"": ""Healthy""
                                          },
                                          ""summary"": {}
                                        }
                                    }";

            var application = new ArgoCdApplicationManifestParser().ParseManifest(appJson);

            application.Spec.Sources.Should()
                       .BeEquivalentTo(new[]
                       {
                           new ApplicationSource()
                           {
                               RepoUrl = new Uri("https://github.com/fake/octopus-test.git"),
                               TargetRevision = "test-branch",
                               Path = "octopus-app"
                           }
                       });
            application.Status.SourceTypes.Should().BeEmpty();
        }
    }
}