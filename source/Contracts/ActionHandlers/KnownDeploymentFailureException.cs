using System;

namespace Octopus.Sashimi.Contracts.ActionHandlers
{
    public class KnownDeploymentFailureException : Exception
    {
        public KnownDeploymentFailureException(string message) : base(message)
        {
        }
    }
}