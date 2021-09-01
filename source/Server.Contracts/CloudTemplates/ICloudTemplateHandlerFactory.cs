using System;

namespace Sashimi.Server.Contracts.CloudTemplates
{
    public interface ICloudTemplateHandlerFactory
    {
        ICloudTemplateHandler GetHandler(string providerId, string template);
    }
}