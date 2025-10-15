using System;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.Common.Commands;
using FluentAssertions;
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

        [TestCase(null, "foo")]
        [TestCase("", "foo")]
        [TestCase("bar", "foo")]
        public void TwoSources_MaxOneUnnamed_Valid(params string[] names)
        {
            var application = CreateApplication(names);
            
            Action action = () => ApplicationSourceValidator.ValidateApplicationSources(application);
            action.Should().NotThrow();
        }
        
        [TestCase("", "")]
        [TestCase(null, null)]
        [TestCase(null, "")]
        [TestCase("foo", "foo")]
        public void TwoSources_DuplicateNames_Throws(params string[] names)
        {
            var application = CreateApplication(names);
            
            Action action = () => ApplicationSourceValidator.ValidateApplicationSources(application);
            action.Should().Throw<CommandException>()
                  .WithMessage($"Application FooApp has multiples sources with the name '{names.First()}'. Please ensure all sources have unique names, only one source is allowed to omit the 'name' property.");
        }
        
        static Application CreateApplication(params string[] names)
        {
            return new Application()
            {
                Metadata = new Metadata()
                {
                    Name = "FooApp"
                },
                Spec = new ApplicationSpec()
                {
                    Sources = names.Select(n => new BasicSource { Name = n }).ToList<SourceBase>()
                }
            };
        }
    }
}