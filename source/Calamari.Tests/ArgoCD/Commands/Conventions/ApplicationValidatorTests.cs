using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    [TestFixture]
    public class ApplicationValidatorTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("bar")]
        public void SingleSource_AnyNameIsFine(params string[] names)
        {
            var application = CreateApplication(names);

            var result = ApplicationValidator.Validate(application);
            result.Errors.Should().BeEmpty();
        }

        [TestCase(null, "foo", "")]
        [TestCase("", "foo", "")]
        [TestCase("bar", "foo", null)]
        public void MultipleSources_ManyUnnamed_Valid(params string[] names)
        {
            var application = CreateApplication(names);

            var result = ApplicationValidator.Validate(application);
            result.Errors.Should().BeEmpty();
        }

        [TestCase("foo", "foo")]
        [TestCase("bar", "foo", "bar", "")]
        public void MultipleSources_DuplicateNames_Throws(params string[] names)
        {
            var application = CreateApplication(names);

            var result = ApplicationValidator.Validate(application);
            result.Errors.Should().BeEquivalentTo($"Application FooApp has multiples sources with the name '{names.First()}'. Please ensure all sources have unique names.");
        }

         [Test]
        public void NoAnnotations_Multisource_NoWarning()
        {
            var application = CreateApplication(new Dictionary<string, string>(), "", "foo");

            var result = ApplicationValidator.Validate(application);
            result.Warnings.Should().BeEmpty();
        }
        
        [Test]
        public void UnnamedAnnotations_Multisource_HasWarning()
        {
            var application = CreateApplication(new Dictionary<string, string>()
            {
                ["argo.octopus.com/project"] = "project-a",
                ["argo.octopus.com/environment"] = "environment-a",
                ["argo.octopus.com/tenant"] = "tenant-a",
            }, "", "foo");

            var result = ApplicationValidator.Validate(application);
            result.Warnings.Should().BeEquivalentTo("The application 'FooApp' requires all annotations to be qualified by source name since it contains multiple sources. Found these unqualified annotations: 'argo.octopus.com/project', 'argo.octopus.com/environment', 'argo.octopus.com/tenant'.");
        }
        
        [Test]
        public void NamedAnnotations_Multisource_NoWarning()
        {
            var application = CreateApplication(new Dictionary<string, string>()
            {
                ["argo.octopus.com/project.foo"] = "project-a",
                ["argo.octopus.com/environment.foo"] = "environment-a",
                ["argo.octopus.com/tenant.foo"] = "tenant-a",
            }, "", "foo");

            var result = ApplicationValidator.Validate(application);
            result.Warnings.Should().BeEmpty();
        }

        static Application CreateApplication(params string[] names)
        {
            return CreateApplication(new Dictionary<string, string>(), names);
        }

        static Application CreateApplication(Dictionary<string, string> annotations, params string[] names)
        {
            return new ArgoCDApplicationBuilder()
                   .WithName("FooApp")
                   .WithAnnotations(annotations)
                   .WithSources(names.Select(n => new ApplicationSource { Name = n, SourceType = SourceType.Directory}).ToList<ApplicationSource>())
                   .Build();
        }
    }
}