using System;

namespace Calamari.ArgoCD
{
    public class ArgoCDInstanceLinks
    {
        readonly string webUiUri;

        public ArgoCDInstanceLinks(string webUIUri)
        {
            webUiUri = webUIUri.TrimEnd('/');
        }

        public string ApplicationDetails(string name, string kubernetesNamespace)
        {
            return $"{webUiUri}/applications/{kubernetesNamespace}/{name}";
        }
    }
}
