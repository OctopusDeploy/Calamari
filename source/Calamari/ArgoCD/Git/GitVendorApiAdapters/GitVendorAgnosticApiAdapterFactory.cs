using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    /// <summary>
    /// Using all registered <see cref="IGitVendorApiAdapterFactory"/> instances, resolves the correct adapter based on which self-reports as being able to utilize the provided connection details.
    /// Returns the first matching adapter in the order provided in the constructor.
    /// </summary>
    public interface IGitVendorAgnosticApiAdapterFactory : IGitVendorApiAdapterFactory
    {
    }
    
    public class GitVendorAgnosticApiAdapterFactory : IGitVendorAgnosticApiAdapterFactory
    {
        readonly IEnumerable<IResolvingGitVendorApiAdapterFactory> gitVendorAdapterFactories;

        public GitVendorAgnosticApiAdapterFactory(IEnumerable<IResolvingGitVendorApiAdapterFactory> gitVendorAdapterFactories)
        {
            this.gitVendorAdapterFactories = gitVendorAdapterFactories;
        }

        public IGitVendorApiAdapter? Create(IRepositoryConnection repositoryConnection)
        {
            //perform a cheap url-based check
            var handlingFactory = gitVendorAdapterFactories.FirstOrDefault(gvaf => gvaf.CanHandleAsCloudHosted(repositoryConnection));
            if (handlingFactory == null)
            {
                //if the cheap check failed - poll the url to determine the service via http
                foreach (var clientFactory in gitVendorAdapterFactories)
                {
                    if (clientFactory.CanHandleAsSelfHosted(repositoryConnection))
                    {
                        handlingFactory = clientFactory;
                        break;
                    }
                }
            }

            return handlingFactory is not null
                ? handlingFactory.Create(repositoryConnection)
                : null;
        }
    }
}
