using System;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using Calamari.Tests.KubernetesFixtures.Builders;
using Calamari.Tests.KubernetesFixtures.ResourceStatus;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class ManifestParserTests
    {
        readonly IKubernetesManifestNamespaceResolver namespaceResolver;
        readonly IVariables variables;
        readonly ILog log;

        public ManifestParserTests()
        {
            log = new InMemoryLog();
            namespaceResolver = new KubernetesManifestNamespaceResolver(new ApiResourcesScopeLookupBuilder()
                                                                            .Build(),
                                                                        log);

            variables = new CalamariVariables();
        }

        [Test]
        public void ShouldGenerateCorrectIdentifiers()
        {
            var input = TestFileLoader.Load("single-deployment.yaml");
            var got = ManifestParser.GetResourcesFromManifest(input, namespaceResolver, variables, log);
            var expected = new[]
            {
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1,
                                       "nginx",
                                       "test")
            };

            got.Should().BeEquivalentTo(expected);
        }

        [Test]
        public void ShouldOmitDefinitionIfTheMetadataSectionIsNotSet()
        {
            var input = TestFileLoader.Load("invalid.yaml");
            var got = ManifestParser.GetResourcesFromManifest(input, namespaceResolver, variables, log);
            got.Should().BeEmpty();
        }

        [Test]
        public void ShouldHandleMultipleResourcesDefinedInTheSameFile()
        {
            var input = TestFileLoader.Load("multiple-resources.yaml");
            var got = ManifestParser.GetResourcesFromManifest(input, namespaceResolver, variables, log);
            var expected = new[]
            {
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "nginx", "default"),
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.ConfigMapV1, "config", "default"),
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.PodV1, "curl", "default")
            };

            got.Should().BeEquivalentTo(expected);
        }

        [Test]
        public void ShouldUseDefaultNamespaceWhenNoNamespaceIsSuppliedInYaml()
        {
            const string defaultNamespace = "DefaultNamespace";
            variables.Set(SpecialVariables.Namespace, defaultNamespace);
            var input = TestFileLoader.Load("no-namespace.yaml");
            var got = ManifestParser.GetResourcesFromManifest(input, namespaceResolver, variables, log);
            var expected = new[]
            {
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1,
                                       "nginx",
                                       defaultNamespace
                                      )
            };

            got.Should().BeEquivalentTo(expected);
        }
        
        [Test]
        public void ShouldHandleInvalidYamlSyntaxInManifest()
        {
            var input = TestFileLoader.Load("invalid-syntax.yaml");
            var got = ManifestParser.GetResourcesFromManifest(input, namespaceResolver, variables, log);
            got.Should().BeEmpty();
        }
    }
}