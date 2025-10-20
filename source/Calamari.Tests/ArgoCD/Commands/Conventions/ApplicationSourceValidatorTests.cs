using System;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Commands;
using FluentAssertions;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    [TestFixture]
    public class ApplicationSourceValidatorTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("bar")]
        public void SingleSource_AnyNameIsFine(params string[] names)
        {
            var application = CreateApplication(names);

            Action action = () => ApplicationSourceValidator.ValidateApplicationSources(application);
            action.Should().NotThrow();
        }

        [TestCase(null, "foo", "")]
        [TestCase("", "foo", "")]
        [TestCase("bar", "foo", null)]
        public void MultipleSources_ManyUnnamed_Valid(params string[] names)
        {
            var application = CreateApplication(names);

            Action action = () => ApplicationSourceValidator.ValidateApplicationSources(application);
            action.Should().NotThrow();
        }

        [TestCase("foo", "foo")]
        [TestCase("bar", "foo", "bar", "")]
        public void MultipleSources_DuplicateNames_Throws(params string[] names)
        {
            var application = CreateApplication(names);

            Action action = () => ApplicationSourceValidator.ValidateApplicationSources(application);
            action.Should()
                  .Throw<CommandException>()
                  .WithMessage($"Application FooApp has multiples sources with the name '{names.First()}'. Please ensure all sources have unique names.");
        }

        static Application CreateApplication(params string[] names)
        {
            return new ArgoCDApplicationBuilder()
                .WithName("FooApp")
                .WithSources(names.Select(n => new BasicSource { Name = n }).ToList<SourceBase>())
                .Build();
        }
    }
}