using System.Collections.Generic;
using FluentAssertions;
using FluentValidation.TestHelper;
using NUnit.Framework;
using Octopus.Server.Extensibility.HostServices.Model;
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
            var context = CreateBareAction(new Dictionary<string, string>
            {
                { SpecialVariables.Action.GoogleCloud.AccountVariable, "Accounts-Variable" },
                { SpecialVariables.Action.GoogleCloud.UseVMServiceAccount, "False"}
            });
            var result = validator.TestValidate(context);
        
            result.Errors.Should().NotContain(f => f.PropertyName == SpecialVariables.Action.GoogleCloud.AccountVariable);
        }
        
        [Test]
        public void Validate_NoAccount_Error()
        {
            var context = CreateBareAction(new Dictionary<string, string>
            {
                { SpecialVariables.Action.GoogleCloud.UseVMServiceAccount, "False"}
            });
            var result = validator.TestValidate(context);
        
            result.Errors.Should().Contain(f => f.PropertyName == SpecialVariables.Action.GoogleCloud.AccountVariable);
        }
        
        [Test]
        public void Validate_WhenUsingVMAccount_NoAccountIsRequired()
        {
            var context = CreateBareAction(new Dictionary<string, string>
            {
                { SpecialVariables.Action.GoogleCloud.UseVMServiceAccount, "True"}
            });
            var result = validator.TestValidate(context);
        
            result.Errors.Should().NotContain(f => f.PropertyName == SpecialVariables.Action.GoogleCloud.AccountVariable);
        }
        
        [Test]
        public void Validate_WhenImpersonateServiceAccount_EmailsAreRequired()
        {
            var context = CreateBareAction(new Dictionary<string, string>
            {
                { SpecialVariables.Action.GoogleCloud.ImpersonateServiceAccount, "True"}
            });
            var result = validator.TestValidate(context);
        
            result.Errors.Should().Contain(f => f.PropertyName == SpecialVariables.Action.GoogleCloud.ServiceAccountEmails);
        }

        static DeploymentActionValidationContext CreateBareAction(IReadOnlyDictionary<string, string> props)
        {
            return new DeploymentActionValidationContext(SpecialVariables.Action.GoogleCloud.ActionTypeName,
                props,
                new List<PackageReference>());
        }
    }
}