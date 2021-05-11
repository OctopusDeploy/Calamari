using FluentValidation;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers.Validation;
using PropertiesDictionary = System.Collections.Generic.IReadOnlyDictionary<string, string>;

namespace Sashimi.GoogleCloud.Accounts.Validation
{
    public abstract class GoogleCloudDeploymentValidatorBase : IDeploymentActionValidator
    {
        readonly string actionType;

        protected GoogleCloudDeploymentValidatorBase(string actionType)
        {
            this.actionType = actionType;
        }

        protected bool ThisAction(DeploymentActionValidationContext arg)
            => arg.ActionType == actionType;

        public virtual void AddDeploymentValidationRule(AbstractValidator<DeploymentActionValidationContext> validator)
        {
            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(SpecialVariables.Action.GoogleCloud.AccountEmail, "Please provide the service account email.")
                .When(ThisAction);

            validator.RuleFor(a => a.Properties)
                .MustHaveProperty(SpecialVariables.Action.GoogleCloud.JsonKey, $"Please provide the json credential.")
                .When(ThisAction);
        }

        protected static bool ScriptIsFromPackage(PropertiesDictionary properties)
        {
            return properties.TryGetValue(KnownVariables.Action.Script.ScriptSource, out var scriptSource) &&
                scriptSource == KnownVariableValues.Action.Script.ScriptSource.Package;
        }
    }
}