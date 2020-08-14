using System;
using System.Collections.Generic;
using FluentAssertions;
using FluentValidation.TestHelper;
using NUnit.Framework;
using Octopus.Server.Extensibility.HostServices.Model;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.AzureScripting.Tests
{
    [TestFixture]
    public class AzureScriptDeploymentActionValidatorFixture
    {
        AzurePowerShellActionHandlerValidator validator;

        [SetUp]
        public void Setup()
        {
            validator = new AzurePowerShellActionHandlerValidator();
        }

        [Test]
        public void Validate_HasAccount_NoErrors()
        {
            var context = CreateBareAction(new Dictionary<string, string> { { SpecialVariables.Action.Azure.AccountId, "Accounts-1" } });
            var result = validator.TestValidate(context);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().NotContain(f => f.PropertyName == SpecialVariables.Action.Azure.AccountId);
        }

        [Test]
        public void Validate_NoAccount_Error()
        {
            var context = CreateBareAction(new Dictionary<string, string>());
            var result = validator.TestValidate(context);

            result.IsValid.Should().BeFalse();

            result.Errors.Should().Contain(f => f.PropertyName == SpecialVariables.Action.Azure.AccountId);
        }

        static DeploymentActionValidationContext CreateBareAction(IReadOnlyDictionary<string, string> props)
        {
            return new DeploymentActionValidationContext(SpecialVariables.Action.Azure.ActionTypeName,
                                                         props,
                                                         new List<PackageReference>());
        }
    }
}