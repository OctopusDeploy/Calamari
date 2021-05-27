using System.Collections.Generic;
using FluentAssertions;
using FluentValidation.TestHelper;
using NUnit.Framework;
using Octopus.Server.Extensibility.HostServices.Model;
using Sashimi.GCPScripting;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.GoogleCloud.Scripting.Tests
{
    [TestFixture]
    public class GoogleCloudActionHandlerValidatorFixture
    {
        GoogleCloudActionHandlerValidator? validator;

        [SetUp]
        public void Setup()
        {
            validator = new GoogleCloudActionHandlerValidator();
        }

        [Test]
        public void Validate_HasAccount_NoErrors()
        {
            var context = CreateBareAction(new Dictionary<string, string> { { SpecialVariables.Action.GoogleCloud.AccountId, "Accounts-1" } });
            var result = validator.TestValidate(context);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().NotContain(f => f.PropertyName == SpecialVariables.Action.GoogleCloud.AccountId);
        }

        [Test]
        public void Validate_NoAccount_Error()
        {
            var context = CreateBareAction(new Dictionary<string, string>());
            var result = validator.TestValidate(context);

            result.IsValid.Should().BeFalse();

            result.Errors.Should().Contain(f => f.PropertyName == SpecialVariables.Action.GoogleCloud.AccountId);
        }

        static DeploymentActionValidationContext CreateBareAction(IReadOnlyDictionary<string, string> props)
        {
            return new DeploymentActionValidationContext(SpecialVariables.Action.GoogleCloud.ActionTypeName,
                props,
                new List<PackageReference>());
        }
    }
}