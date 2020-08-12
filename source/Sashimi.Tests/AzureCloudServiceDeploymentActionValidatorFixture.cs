using System;
using System.Collections.Generic;
using FluentAssertions;
using FluentValidation.TestHelper;
using NUnit.Framework;
using Octopus.Server.Extensibility.HostServices.Model;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.AzureCloudService.Tests
{
    public class AzureCloudServiceDeploymentActionValidatorFixture
    {
        [Test]
        public void Validate_NoAccountNotLegacyMode_NoErrors()
        {
            var context = CreateBareAction(new Dictionary<string, string>());
            var result = new AzureCloudServiceActionHandlerValidator().TestValidate(context);

            result.Errors.Should().NotContain(f => f.PropertyName == SpecialVariables.Action.Azure.AccountId);
        }

        [Test]
        public void Validate_HasAccountLegacyMode_NoErrors()
        {
            var context = CreateBareAction(new Dictionary<string, string>
            {
                { SpecialVariables.Action.Azure.IsLegacyMode, bool.TrueString },
                { SpecialVariables.Action.Azure.AccountId, "Accounts-1" }
            });
            var result = new AzureCloudServiceActionHandlerValidator().TestValidate(context);

            result.Errors.Should().NotContain(f => f.PropertyName == SpecialVariables.Action.Azure.AccountId);
        }

        [Test]
        public void Validate_NoAccountLegacyMode_Error()
        {
            var context = CreateBareAction(new Dictionary<string, string>
            {
                { SpecialVariables.Action.Azure.IsLegacyMode, bool.TrueString }
            });
            var result = new AzureCloudServiceActionHandlerValidator().TestValidate(context);

            result.Errors.Should().Contain(f => f.PropertyName == SpecialVariables.Action.Azure.AccountId);
        }

        [Test]
        public void ShouldRequireAccountForAzureCloudService()
        {
            var context = CreateBareAction(new Dictionary<string, string>
            {
                { SpecialVariables.Action.Azure.AccountId, "boo" },
                { SpecialVariables.Action.Azure.CloudServiceName, "boo" },
                { SpecialVariables.Action.Azure.StorageAccountName, "boo" },
                { SpecialVariables.Action.Azure.SwapIfPossible, bool.FalseString },
                { SpecialVariables.Action.Azure.Slot, "Staging" },
                { SpecialVariables.Action.Azure.UseCurrentInstanceCount, bool.TrueString }
            });
            var result = new AzureCloudServiceActionHandlerValidator().TestValidate(context);

            result.IsValid.Should().BeTrue();
        }

        [Test]
        public void ShouldNotRequireAccountForAzureCloudServiceFrom26()
        {
            var context = CreateBareAction(new Dictionary<string, string>
            {
                { SpecialVariables.Action.Azure.SubscriptionId, "boo" },
                { SpecialVariables.Action.Azure.CloudServiceName, "boo" },
                { SpecialVariables.Action.Azure.StorageAccountName, "boo" },
                { SpecialVariables.Action.Azure.SwapIfPossible, bool.FalseString },
                { SpecialVariables.Action.Azure.Slot, "Staging" },
                { SpecialVariables.Action.Azure.UseCurrentInstanceCount, bool.TrueString }
            });
            var result = new AzureCloudServiceActionHandlerValidator().TestValidate(context);

            result.IsValid.Should().BeTrue();
        }

        static DeploymentActionValidationContext CreateBareAction(Dictionary<string, string> properties)
        {
            return new DeploymentActionValidationContext(SpecialVariables.Action.Azure.CloudServiceActionTypeName,
                                                         properties,
                                                         new List<PackageReference>());
        }

    }
}