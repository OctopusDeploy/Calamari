using System;

namespace Octopus.Sashimi.Contracts.CloudTemplates
{
    public interface ICloudTemplateHandlerFactory
    {
        ICloudTemplateHandler GetHandler(string providerId, string template);
    }
}