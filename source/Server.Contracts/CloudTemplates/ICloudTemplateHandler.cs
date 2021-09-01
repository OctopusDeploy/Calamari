using System;
using Octopus.Server.Extensibility.Metadata;

namespace Sashimi.Server.Contracts.CloudTemplates
{
    public interface ICloudTemplateHandler
    {
        bool CanHandleTemplate(string providerId, string template);

        Metadata ParseTypes(string template);

        object ParseModel(string template);
    }
}