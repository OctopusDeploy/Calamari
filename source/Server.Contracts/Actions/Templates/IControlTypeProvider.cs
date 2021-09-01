using System;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Server.Contracts.Actions.Templates
{
    public interface IControlTypeProvider
    {
        ControlType ControlType { get; }
        VariableType VariableType { get; }
    }
}