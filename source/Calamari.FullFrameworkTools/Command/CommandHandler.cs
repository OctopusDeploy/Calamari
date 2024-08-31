#nullable enable
using System;
using Calamari.FullFrameworkTools.Iis;
using Calamari.FullFrameworkTools.WindowsCertStore;
using Calamari.FullFrameworkTools.WindowsCertStore.Contracts;

namespace Calamari.FullFrameworkTools.Command;

public interface ICommandHandler
{
    object Handle(IRequest obj);
}

public class CommandHandler : ICommandHandler
{
    readonly IWindowsX509CertificateStore certificateStore;
    readonly IInternetInformationServer internetInformationServer;

    public CommandHandler(IWindowsX509CertificateStore certificateStore, IInternetInformationServer internetInformationServer)
    {
        this.certificateStore = certificateStore;
        this.internetInformationServer = internetInformationServer;
    }

    public object Handle(IRequest obj)
    {
        return obj switch
               {
                   null => throw new NotImplementedException(),

                   //IIS Comments
                   OverwriteHomeDirectoryRequest req => req.DoIt(internetInformationServer),

                   // Windows Cert Commands
                   FindCertificateStoreRequest req => req.DoIt(certificateStore),
                   ImportCertificateToStoreByUserRequest req => req.DoIt(certificateStore),
                   ImportCertificateToStoreByLocationRequest req => req.DoIt(certificateStore),
                   AddPrivateKeyAccessRulesRequest req => req.DoIt(certificateStore),

                   _ => throw new ArgumentOutOfRangeException($"Unknown Request {obj.GetType().Name}"),
               };
    }
}

public interface IFullFrameworkToolResponse
    {
    }

    public class StringResponse : IFullFrameworkToolResponse { public string? Value { get; set; } }
    public class VoidResponse : IFullFrameworkToolResponse { }
    public class BoolResponse : IFullFrameworkToolResponse { public bool Value { get; set; } }
