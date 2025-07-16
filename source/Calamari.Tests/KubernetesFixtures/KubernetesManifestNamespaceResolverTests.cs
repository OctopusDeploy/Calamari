using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using YamlDotNet.RepresentationModel;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class KubernetesManifestNamespaceResolverTests
    {
        KubernetesManifestNamespaceResolver sut;
        IApiResourceScopeLookup apiResourceScopeLookup;

        [SetUp]
        public void SetUp()
        {
            apiResourceScopeLookup = Substitute.For<IApiResourceScopeLookup>();
            var memoryLog = new InMemoryLog();
            sut = new KubernetesManifestNamespaceResolver(apiResourceScopeLookup, memoryLog);
        }

        [TestCaseSource(nameof(ResolveNamespaceTestData))]
        public void ResolveNamespace_ShouldResolveExpectedNamespace(string yaml, bool isKnownResourceType, bool isNamespacedResourceType, CalamariVariables variables, string expectedNamespace)
        {
            // Arrange
            apiResourceScopeLookup.TryGetIsNamespaceScoped(Arg.Any<ApiResourceIdentifier>(), out var outParam)
                                  .Returns(ci =>
                                           {
                                               ci[1] = isNamespacedResourceType;
                                               return isKnownResourceType;
                                           });
            
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(yaml));
            var rootNode = (YamlMappingNode)yamlStream.Documents.First().RootNode;
            
            
            // Act
            var @namespace = sut.ResolveNamespace(rootNode, variables ?? new CalamariVariables());
            
            // Assert
            @namespace.Should().Be(expectedNamespace);
        }

        public static object[] ResolveNamespaceTestData = {
            //Namespaced resources
            new TestCaseData(@"
metadata:
  name: resource
  namespace: ABC",
                             true,
                             true,
                             null,
                             "ABC"
                            ).SetName("Known namespaced resource + namespace in manifest = Manifest namespace"),
            new TestCaseData(@"
metadata:
  name: resource",
                             true,
                             true,
                             new CalamariVariables
                             {
                                 {SpecialVariables.Helm.Namespace, "DEF"}
                             },
                             "DEF"
                            ).SetName("Known namespaced resource + namespace not in manifest + helm namespace variable = helm namespace"),
            new TestCaseData(@"
metadata:
  name: resource",
                             true,
                             true,
                             new CalamariVariables
                             {
                                 {SpecialVariables.Namespace, "GHI"}
                             },
                             "GHI"
                            ).SetName("Known namespaced resource + namespace not in manifest + namespace variable = variable namespace"),
            new TestCaseData(@"
metadata:
  name: resource",
                             true,
                             true,
                             null,
                             "default"
                            ).SetName("Known namespaced resource + namespace not in manifest + no namespace variable = default namespace"),
            new TestCaseData(@"
metadata:
  name: resource
  namespace: ABC123",
                             false, //unknown resource
                             true,
                             null,
                             "ABC123"
                            ).SetName("Unknown namespaced resource + namespace in manifest = manifest namespace used"),
            new TestCaseData(@"
metadata:
  name: resource",
                             false, //unknown resource
                             true,
                             new CalamariVariables
                             {
                                 { SpecialVariables.Namespace, "GHI123" }
                             },
                             "GHI123"
                            ).SetName("Unknown namespaced resource + namespace not in manifest + namespace variable = variable namespace"),
            //Non-namespaced resources
                        new TestCaseData(@"
metadata:
  name: resource
  namespace: ABC",
                             true,
                             false, //non-namespaced
                             null,
                             null
                            ).SetName("Known non-namespaced resource + namespace in manifest = null namespace"),
            new TestCaseData(@"
metadata:
  name: resource",
                             true,
                             false, //non-namespaced
                             null,
                             null
                            ).SetName("Known non-namespaced resource + namespace not in manifest = null namespace"),
            new TestCaseData(@"
metadata:
  name: resource
  namespace: ABC123",
                             false, //unknown resource
                             false, //non-namespaced
                             null,
                             "ABC123"
                            ).SetName("Unknown non-namespaced resource + namespace in manifest = manifest namespace used"),
            new TestCaseData(@"
metadata:
  name: resource",
                             false, //unknown resource
                             false, //non-namespaced
                             null,
                             "default"
                            ).SetName("Unknown non-namespaced resource + namespace not in manifest = default namespace"),
            new TestCaseData(@"
metadata:
  name: resource",
                             false, //unknown resource
                             false, //non-namespaced
                             new CalamariVariables
                             {
                                 { SpecialVariables.Namespace, "GHI123" }
                             },
                             "GHI123"
                            ).SetName("Unknown non-namespaced resource + namespace not in manifest + namespace variable = variable namespace")
            
        };
    }
}