﻿using System;
using Octopus.CoreUtilities;
using Sashimi.AzureScripting;
using Sashimi.AzureWebApp.Endpoints;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureWebApp
{
    class AzureWebAppActionHandler : IActionHandlerWithAccount
    {
        public string Id => SpecialVariables.Action.Azure.WebAppActionTypeName;
        public string Name => "Deploy an Azure Web App";
        public string Description => "Deploy the contents of a package to an Azure Web App.";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => true;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, AzureConstants.AzureActionHandlerCategory, ActionHandlerCategory.Package };
        public string[] StepBasedVariableNameForAccountIds { get; } = {SpecialVariables.Action.Azure.AccountId};

        public IActionHandlerResult Execute(IActionHandlerContext context)
        {
            var isLegacyAction = !string.IsNullOrWhiteSpace(context.Variables.Get(SpecialVariables.Action.Azure.AccountId));

            if (!isLegacyAction && context.DeploymentTargetType.Some())
            {
                if (context.DeploymentTargetType.Value != AzureWebAppEndpoint.AzureWebAppDeploymentTargetType)
                    throw new ControlledActionFailedException($"The machine {context.DeploymentTargetName.SomeOr("<unknown>")} will not be deployed to because it is not an {AzureWebAppEndpoint.AzureWebAppDeploymentTargetType.DisplayName} target.");
            }

            return context.CalamariCommand(AzureConstants.CalamariAzure, "deploy-azure-web")
                          .WithCheckAccountIsNotManagementCertificate(context)
                          .WithAzureTools(context)
                          .WithStagedPackageArgument()
                          .Execute();
        }
    }
}