#nullable enable
using System;
using Calamari.FullFrameworkTools.Command;
using Calamari.FullFrameworkTools.Iis;

namespace Calamari.FullFrameworkTools.WindowsCertStore.Contracts;

public class OverwriteHomeDirectoryRequest : IRequest
{
    public OverwriteHomeDirectoryRequest(string iisWebSiteName, string path, bool legacySupport)
    {
        IisWebSiteName = iisWebSiteName;
        Path = path;
        LegacySupport = legacySupport;
    }

    public string IisWebSiteName { get; set; }
    public string Path { get; set; }
    public bool LegacySupport { get; set; }
    
    public BoolResponse DoIt(IInternetInformationServer certificateStore)
    {
        certificateStore.OverwriteHomeDirectory(IisWebSiteName, Path, LegacySupport);
        return new BoolResponse();
    }
}