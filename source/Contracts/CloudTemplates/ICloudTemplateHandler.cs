using System;
using Octopus.Server.Extensibility.Metadata;

namespace Octopus.Sashimi.Contracts.CloudTemplates
{
    public interface ICloudTemplateHandler
    {
        bool CanHandleTemplate(string providerId, string template);

        Metadata ParseTypes(string template);

        object ParseModel(string template);
    }
}