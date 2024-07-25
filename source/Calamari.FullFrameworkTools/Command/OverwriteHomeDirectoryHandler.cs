using System;
using Calamari.FullFrameworkTools.Iis;

namespace Calamari.FullFrameworkTools.Command
{
    public class OverwriteHomeDirectoryHandler : FullFrameworkToolCommandHandler<OverwriteHomeDirectoryRequest, OverwriteHomeDirectoryResponse>
    {
        public string Name { get; }

        protected override OverwriteHomeDirectoryResponse Handle(OverwriteHomeDirectoryRequest request)
        {
            var iis = new InternetInformationServer();
            var result = iis.OverwriteHomeDirectory(request.IisWebSiteName, request.Path, request.LegacySupport);
            return new OverwriteHomeDirectoryResponse(result);
        }
    }
    
    public class OverwriteHomeDirectoryRequest : IFullFrameworkToolRequest
    {
        public string IisWebSiteName { get; set; }
        public string Path { get; set; }
        public bool LegacySupport { get; set; }
    }

    public class OverwriteHomeDirectoryResponse : IFullFrameworkToolResponse
    {
        public OverwriteHomeDirectoryResponse(bool result)
        {
            Result = result;
        }

        public bool Result { get; set; }
    }
}