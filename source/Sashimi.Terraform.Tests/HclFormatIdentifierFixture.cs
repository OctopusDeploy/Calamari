using System;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using Sashimi.Terraform.CloudTemplates;
using Sashimi.Tests.Shared;
using Sashimi.Tests.Shared.Extensions;

namespace Sashimi.Terraform.Tests
{
    public class HclFormatIdentifierFixture
    {
        [Test]
        public void CanIdentify()
        {
            var template = File.ReadAllText(TestEnvironment.GetTestPath("HclFormatIdentifierFixture_main.tf"));
            HclFormatIdentifier.IsHcl(template)
                .Should().BeTrue();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("[]")]
        public void DoesNotExplodeOnInvalid(string template)
        {
            HclFormatIdentifier.IsHcl(template)
                .Should().BeFalse();
        }
    }
}