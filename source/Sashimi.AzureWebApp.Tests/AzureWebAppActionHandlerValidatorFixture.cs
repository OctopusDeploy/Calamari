using System;
using System.Collections.Generic;
using FluentAssertions;
using FluentValidation.TestHelper;
using NUnit.Framework;
using Octopus.Server.Extensibility.HostServices.Model;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.AzureWebApp.Tests
{
    public class AzureWebAppActionHandlerValidatorFixture
    {
        AzureWebAppActionHandlerValidator validator;

        [SetUp]
        public void Setup()
        {
            validator = new AzureWebAppActionHandlerValidator();
        }

        [Test]
        public void Validate_Defaults_No_Error()
        {
            var context = new DeploymentActionValidationContext(SpecialVariables.Action.Azure.WebAppActionTypeName,
                                                                new Dictionary<string, string>(),
                                                                new List<PackageReference>());

            var result = validator.TestValidate(context);

            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void Validate_LegacyMode_On_Error()
        {
            var context = new DeploymentActionValidationContext(SpecialVariables.Action.Azure.WebAppActionTypeName,
                                                                new Dictionary<string, string> {{SpecialVariables.Action.Azure.IsLegacyMode, bool.TrueString}},
                                                                new List<PackageReference>());

            var result = validator.TestValidate(context);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(SpecialVariables.Action.Azure.AccountId);
        }

        [Test]
        public void Validate_LegacyMode_On_With_Account_Error()
        {
            var context = new DeploymentActionValidationContext(SpecialVariables.Action.Azure.WebAppActionTypeName,
                                                                new Dictionary<string, string>
                                                                {
                                                                    {SpecialVariables.Action.Azure.IsLegacyMode, bool.TrueString},
                                                                    {SpecialVariables.Action.Azure.AccountId, "myaccount"}
                                                                },
                                                                new List<PackageReference>());

            var result = validator.TestValidate(context);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(SpecialVariables.Action.Azure.WebAppName);
        }

        [Test]
        public void Validate_LegacyMode_On_With_Account_And_WebAppName_No_Error()
        {
            var context = new DeploymentActionValidationContext(SpecialVariables.Action.Azure.WebAppActionTypeName,
                                                                new Dictionary<string, string>
                                                                {
                                                                    {SpecialVariables.Action.Azure.IsLegacyMode, bool.TrueString},
                                                                    {SpecialVariables.Action.Azure.AccountId, "myaccount"},
                                                                    {SpecialVariables.Action.Azure.WebAppName, "MyApp"}
                                                                },
                                                                new List<PackageReference>());

            var result = validator.TestValidate(context);

            result.IsValid.Should().BeTrue();
        }
    }
}