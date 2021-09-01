using System;
using System.Linq;
using Sashimi.Server.Contracts.CloudTemplates;

namespace Sashimi.Tests.Shared.Server
{
    public class TestCloudTemplateHandlerFactory : ICloudTemplateHandlerFactory
    {
        readonly ICloudTemplateHandler[] cloudTemplateHandler;

        public TestCloudTemplateHandlerFactory(params ICloudTemplateHandler[] cloudTemplateHandler)
        {
            this.cloudTemplateHandler = cloudTemplateHandler;
        }

        public ICloudTemplateHandler GetHandler(string providerId, string template)
        {
            return cloudTemplateHandler.First(t => t.CanHandleTemplate(providerId, template));
        }
    }
}