using FluentValidation;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers.Validation;
using PropertiesDictionary = System.Collections.Generic.IReadOnlyDictionary<string, string>;

namespace Sashimi.GCP.Accounts.Validation
{
    public abstract class GcpDeploymentValidatorBase : IDeploymentActionValidator
    {
        readonly string actionType;

        protected GcpDeploymentValidatorBase(string actionType)
        {
            this.actionType = actionType;
        }

        protected bool ThisAction(DeploymentActionValidationContext arg)
            => arg.ActionType == actionType;

        public virtual void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(SpecialVariables.Action.Gcp.ServiceAccountEmail, "Please provide the service account email.")
                .When(ThisAction);

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(SpecialVariables.Action.Gcp.Json, $"Please provide the json credential.")
                .When(ThisAction);
        }

        protected static bool ScriptIsFromPackage(PropertiesDictionary properties)
        {
            return properties.TryGetValue(KnownVariables.Action.Script.ScriptSource, out var scriptSource) &&
                scriptSource == KnownVariableValues.Action.Script.ScriptSource.Package;
        }
    }
}