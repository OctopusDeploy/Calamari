using System;
using Sashimi.Aws.Common.Variables;
using Sashimi.Server.Contracts.Actions.Templates;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Aws.Common.ControlTypes
{
    class AmazonWebServicesControlTypeProvider : IControlTypeProvider
    {
        public ControlType ControlType => AmazonWebServicesControlType.AmazonWebServicesAccount;
        public VariableType VariableType => AmazonWebServicesVariableType.AmazonWebServicesAccount;
    }
}