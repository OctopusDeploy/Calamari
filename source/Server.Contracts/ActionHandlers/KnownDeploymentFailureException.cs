using System;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public class KnownDeploymentFailureException : Exception
    {
        public KnownDeploymentFailureException(string message) : base(message)
        {
        }
    }
}