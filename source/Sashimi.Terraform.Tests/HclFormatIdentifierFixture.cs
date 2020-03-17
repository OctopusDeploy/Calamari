using System;
using FluentAssertions;
using NUnit.Framework;
using Sashimi.Terraform.CloudTemplates;
using Sashimi.Tests.Shared.Extensions;

namespace Sashimi.Terraform.Tests
{
    public class HclFormatIdentifierFixture
    {
        [Test]
        public void CanIdentify()
        {
            var template = this.ReadResourceAsString("HclFormatIdentifierFixture_main.tf");
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