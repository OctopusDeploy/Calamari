using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Azure.ServiceFabric.Util
{
    /// <summary>
    /// A service for dealing with task cancelled exceptions.
    /// </summary>
    public class TaskCanceledExceptionHandler
    {
        /// <summary>
        /// This methods deals with the situtaion described in
        /// https://github.com/OctopusDeploy/Issues/issues/4408
        /// </summary>
        /// <param name="ex">The exception to extract details from</param>
        public void HandleException(TaskCanceledException ex)
        {
            ex?.Task?.Exception?.InnerExceptions?
                .Select(inner => inner.ToString())
                .Aggregate("", (message, exMessage) => message + "\n" + exMessage)
                .Tee(Log.Error);
        }
    }
}