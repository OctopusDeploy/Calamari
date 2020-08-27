using System;
using FluentValidation;
using Sashimi.Server.Contracts.ActionHandlers.Validation;

namespace Sashimi.NamingIsHard
{
    class MyActionHandlerValidator : AbstractValidator<DeploymentActionValidationContext>
    {
        public MyActionHandlerValidator()
        {
            When(a => a.ActionType == SpecialVariables.MyActionHandlerTypeName,
                 () =>
                 {
                     RuleFor(a => a.Properties)
                         .MustHaveProperty(SpecialVariables.Action.MyProp, "Please enter MyProp");
                 });
        }
    }
}