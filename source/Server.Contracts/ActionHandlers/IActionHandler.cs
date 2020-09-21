using System;
using System.Collections;
using System.Collections.Generic;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    /// <summary>
    /// Implementors of this interface must not keep state so that they can be reusable between steps and deployments
    /// </summary>
    public interface IActionHandler
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        string? Keywords { get; }
        bool ShowInStepTemplatePickerUI { get; }
        bool WhenInAChildStepRunInTheContextOfTheTargetMachine { get; }
        bool CanRunOnDeploymentTarget { get; }
        ActionHandlerCategory[] Categories { get; }

        /// <summary>
        /// For config-as-code projects, various ID properties can be replaced with their name equivalent. This lookup allows us to inspect potential IdOrName properties.
        /// E.g. Octopus.Action.Email.ToTeamIds, Octopus.Action.Azure.AccountId
        /// </summary>
        IEnumerable<string>? NamedPropertiesLookup { get; }

        IActionHandlerResult Execute(IActionHandlerContext context);
    }
}