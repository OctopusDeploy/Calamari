using System.Collections.Generic;
using Calamari.Kubernetes;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Tests.KubernetesFixtures.Builders
{
    public class ApiResourcesScopeLookupBuilder
    {
        Dictionary<ApiResourceIdentifier, bool> namespacedResourcesLookup = new Dictionary<ApiResourceIdentifier, bool>();

        public ApiResourcesScopeLookupBuilder WithNonNamespacedApiResource(ApiResourceIdentifier apiResourceIdentifier)
        {
            namespacedResourcesLookup.Add(apiResourceIdentifier, false);

            return this;
        }

        public ApiResourcesScopeLookupBuilder WithNamespacedApiResource(ApiResourceIdentifier apiResourceIdentifier)
        {
            namespacedResourcesLookup.Add(apiResourceIdentifier, true);

            return this;
        }

        public ApiResourcesScopeLookupBuilder WithoutApiResource(ApiResourceIdentifier apiResourceIdentifier)
        {
            if (namespacedResourcesLookup.ContainsKey(apiResourceIdentifier))
            {
                namespacedResourcesLookup.Remove(apiResourceIdentifier);
            }

            return this;
        }

        public ApiResourcesScopeLookupBuilder WithDefaults()
        {
            namespacedResourcesLookup.AddRangeUnique(ApiResourceScopeLookup.DefaultResourceScopeLookup);
            return this;
        }

        public IApiResourceScopeLookup Build()
        {
            return new StubApiResourceScopeLookup(namespacedResourcesLookup);
        }

        class StubApiResourceScopeLookup : IApiResourceScopeLookup
        {
            readonly Dictionary<ApiResourceIdentifier, bool> lookup;

            public StubApiResourceScopeLookup(Dictionary<ApiResourceIdentifier, bool> lookup)
            {
                this.lookup = lookup;
            }

            public bool TryGetIsNamespaceScoped(ApiResourceIdentifier apiResourceIdentifier, out bool isNamespaceScoped)
            {
                return lookup.TryGetValue(apiResourceIdentifier, out isNamespaceScoped);
            }
        }
    }
}