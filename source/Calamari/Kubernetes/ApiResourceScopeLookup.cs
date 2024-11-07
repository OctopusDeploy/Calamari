using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes
{
    public interface IApiResourceScopeLookup
    {
        bool TryGetIsClusterScoped(ApiResourceIdentifier apiResourceIdentifier, out bool isClusterScoped);
    }

    public class ApiResourceScopeLookup: IApiResourceScopeLookup
    {
        readonly Kubectl kubectl;
        readonly ILog log;
        readonly Lazy<Dictionary<ApiResourceIdentifier, bool>> namespacedApiResourceDictionary;

        public ApiResourceScopeLookup(Kubectl kubectl, ILog log)
        {
            this.kubectl = kubectl;
            this.log = log;
            namespacedApiResourceDictionary = new Lazy<Dictionary<ApiResourceIdentifier, bool>>(GetNamespacedApiResourceDictionary);
        }

        public bool TryGetIsClusterScoped(ApiResourceIdentifier apiResourceIdentifier, out bool isClusterScoped)
        {
            return namespacedApiResourceDictionary.Value.TryGetValue(apiResourceIdentifier, out isClusterScoped);
        }

        Dictionary<ApiResourceIdentifier, bool> GetNamespacedApiResourceDictionary()
        {
            var apiResourceLines = kubectl.ExecuteCommandAndReturnOutput("api-resources");
            apiResourceLines.Result.VerifySuccess();

            var a = apiResourceLines.Output.InfoLogs.First().Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).Reverse().ToArray();
            if (!(a[0] == "KIND" && a[1] == "NAMESPACED" && a[2] == "APIVERSION"))
            {
                log.Error("Unexpected output from kubectl api-resources command, assuming all resources are namespaced.");
                log.Verbose("Output was:" + Environment.NewLine + string.Join(Environment.NewLine, apiResourceLines.Output.InfoLogs));
                return new Dictionary<ApiResourceIdentifier, bool>();
            }


            return apiResourceLines
                   .Output.InfoLogs.Skip(1)
                   .Select(line => line.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).Reverse().ToArray())
                   .Where(parts => parts.Length > 3)
                   .ToDictionary( parts => new ApiResourceIdentifier(parts[2], parts[0]), parts => bool.Parse(parts[1]));
        }

    }
}