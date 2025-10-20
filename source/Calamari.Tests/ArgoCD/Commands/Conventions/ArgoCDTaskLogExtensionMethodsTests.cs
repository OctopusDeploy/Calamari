#if NET
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    [TestFixture]
    public class ArgoCDTaskLogExtensionMethodsTests
    {
        [Test]
        public void NoAnnotations_Multisource_NoWarning()
        {
            InMemoryLog log = new InMemoryLog();
            var application = CreateApplication(new Dictionary<string, string>(), "", "foo");

            log.LogUnnamedAnnotationsInMultiSourceApplication(application);

            log.Messages.Should().BeEmpty();
        }
        
        [Test]
        public void UnnamedAnnotations_Multisource_HasWarning()
        {
            InMemoryLog log = new InMemoryLog();
            var application = CreateApplication(new Dictionary<string, string>()
            {
                ["argo.octopus.com/project"] = "project-a",
                ["argo.octopus.com/environment"] = "environment-a",
                ["argo.octopus.com/tenant"] = "tenant-a",
            }, "", "foo");

            log.LogUnnamedAnnotationsInMultiSourceApplication(application);

            log.Messages.Select(m => m.FormattedMessage).Should().BeEquivalentTo("The application 'FooApp' requires all annotations to be qualified by source name since it contains multiple sources. Found these unqualified annotations: 'argo.octopus.com/project', 'argo.octopus.com/environment', 'argo.octopus.com/tenant'.");
        }
        
        [Test]
        public void NamedAnnotations_Multisource_NoWarning()
        {
            InMemoryLog log = new InMemoryLog();
            var application = CreateApplication(new Dictionary<string, string>()
            {
                ["argo.octopus.com/project.foo"] = "project-a",
                ["argo.octopus.com/environment.foo"] = "environment-a",
                ["argo.octopus.com/tenant.foo"] = "tenant-a",
            }, "", "foo");

            log.LogUnnamedAnnotationsInMultiSourceApplication(application);

            log.Messages.Should().BeEmpty();
        }
        
        static Application CreateApplication(Dictionary<string, string> annotations, params string[] names)
        {
            return new ArgoCDApplicationBuilder()
                   .WithName("FooApp")
                   .WithAnnotations(annotations)
                   .WithSources(names.Select(n => new BasicSource { Name = n }).ToList<SourceBase>())
                   .Build();
        }
    }
}
#endif