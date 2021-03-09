using System;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.AzureScripting;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureResourceGroup
{
    class AzureResourceGroupActionHandler : IActionHandlerWithAccount
    {
        public string Id => SpecialVariables.Action.AzureResourceGroup.ResourceGroupActionTypeName;
        public string Name => "Deploy an Azure Resource Manager template";
        public string Description => "Deploy an Azure Resource Manager (ARM) template.";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => true;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, AzureConstants.AzureActionHandlerCategory, ActionHandlerCategory.Script };
        public string[] StepBasedVariableNameForAccountIds { get; } = {SpecialVariables.Action.Azure.AccountId};

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            var templateInPackage = context.Variables.Get(SpecialVariables.Action.AzureResourceGroup.TemplateSource, String.Empty) == "Package";
            var template = context.Variables.Get(SpecialVariables.Action.AzureResourceGroup.ResourceGroupTemplate)!;
            var templateParameters = GetTemplateParameters();

            var builder = context.CalamariCommand(AzureConstants.CalamariAzure, "deploy-azure-resource-group")
                                 .WithAzureTools(context, taskLog);

            if (templateInPackage)
                builder.WithStagedPackageArgument();
            else
                builder
                    .WithDataFile(template, "template.json")
                    .WithDataFile(templateParameters, "parameters.json");

            return builder.Execute(taskLog);

            string GetTemplateParameters()
            {
                if (templateInPackage)
                {
                    return context.Variables.Get(SpecialVariables.Action.AzureResourceGroup.ResourceGroupTemplateParameters)!;
                }

                var parametersJson = context.Variables.GetRaw(SpecialVariables.Action.AzureResourceGroup.ResourceGroupTemplateParameters)!;

                var parameterMetadata = AzureResourceGroupActionUtils.ExtractParameterTypes(template);
                return AzureResourceGroupActionUtils.TemplateParameters(parametersJson, parameterMetadata, context.Variables);
            }
        }
    }
}