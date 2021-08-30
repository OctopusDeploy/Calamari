using System.Collections.Generic;
using Octopus.Server.Extensibility.Metadata;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.CloudTemplates;

namespace Sashimi.AzureAppService
{
    public class AzureCloudTemplateHandler : ICloudTemplateHandler
    {
        private const string Provider = "AzureAppService";

        private readonly IFormatIdentifier formatIdentifier;

        public AzureCloudTemplateHandler(IFormatIdentifier formatIdentifier)
        {
            this.formatIdentifier = formatIdentifier;
        }
        
        public bool CanHandleTemplate(string providerId, string template)
        {
            return providerId == Provider && (template.Contains("#{") || formatIdentifier.IsJson(template));
        }

        public Metadata ParseTypes(string template)
        {
            return new Metadata();
        }

        public object ParseModel(string template)
        {
            return new Dictionary<string, object>();
        }
    }
}