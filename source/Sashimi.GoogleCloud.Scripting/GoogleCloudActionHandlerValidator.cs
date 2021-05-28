using System.Collections.Generic;
using FluentValidation;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.GoogleCloud.Scripting
{
    class GoogleCloudActionHandlerValidator : AbstractValidator<DeploymentActionValidationContext>
    {
        public GoogleCloudActionHandlerValidator()
        {
            When(a => a.ActionType == SpecialVariables.Action.GoogleCloud.ActionTypeName,
                () =>
                {
                    RuleFor(a => a.Properties)
                        .MustHaveProperty(SpecialVariables.Action.GoogleCloud.AccountVariable, "Please provide a variable expression for the Account ID to use.")
                        .When(a => a.Properties.TryGetValue(SpecialVariables.Action.GoogleCloud.UseVMServiceAccount, out var useVmServiceAccount) && useVmServiceAccount == "False");
                    
                    RuleFor(a => a.Properties)
                        .MustHaveProperty(SpecialVariables.Action.GoogleCloud.ServiceAccountEmails, "Please provide service account email(s) to be impersonated as.")
                        .When(a => a.Properties.TryGetValue(SpecialVariables.Action.GoogleCloud.ImpersonateServiceAccount, out var impersonateServiceAccount) && impersonateServiceAccount == "True");
                    
                   RuleFor(ctx => ctx.Properties)
                        .MustHaveProperty(KnownVariables.Action.Script.ScriptBody, "Please provide the script body to run.")
                        .When(a => !ScriptIsFromPackage(a.Properties));

                    RuleFor(ctx => ctx.Properties)
                        .MustHaveProperty(KnownVariables.Action.Script.ScriptFileName, "Please provide a script file name.")
                        .When(a => ScriptIsFromPackage(a.Properties));
                });
        }

        private static bool ScriptIsFromPackage(IReadOnlyDictionary<string, string> properties)
        {
            return properties.TryGetValue(KnownVariables.Action.Script.ScriptSource, out var scriptSource) &&
                   scriptSource == KnownVariableValues.Action.Script.ScriptSource.Package;
        }
    }
}