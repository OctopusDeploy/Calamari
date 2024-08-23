#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Calamari.FullFrameworkTools.Iis;
using Calamari.FullFrameworkTools.WindowsCertStore.WindowsNative;

namespace Calamari.FullFrameworkTools.WindowsCertStore;

public interface IWindowsX509CertificateStore
{
    string? FindCertificateStore(string thumbprint, StoreLocation storeLocation);

    void ImportCertificateToStore(byte[] pfxBytes,
                                  string password,
                                  StoreLocation storeLocation,
                                  string storeName,
                                  bool privateKeyExportable);

    void AddPrivateKeyAccessRules(string thumbprint,
                                  StoreLocation storeLocation,
                                  ICollection<PrivateKeyAccessRule> privateKeyAccessRules);

    void AddPrivateKeyAccessRules(string thumbprint,
                                  StoreLocation storeLocation,
                                  string storeName,
                                  ICollection<PrivateKeyAccessRule> privateKeyAccessRules);

    void ImportCertificateToStore(byte[] pfxBytes,
                                  string password,
                                  string userName,
                                  string storeName,
                                  bool privateKeyExportable);
}



public class FindCertificateStoreRequest : IRequest
{
    public FindCertificateStoreRequest(string thumbprint, StoreLocation storeLocation)
    {
        Thumbprint = thumbprint;
        StoreLocation = storeLocation;
    }

    public string Thumbprint { get; }
    public StoreLocation StoreLocation { get; }
    
    public VoidResponse DoIt(IWindowsX509CertificateStore certificateStore)
    {
        certificateStore.FindCertificateStore(Thumbprint, StoreLocation);
        return new VoidResponse();
    }
}


public class ImportCertificateToStoreByUserRequest : IRequest
{
    public ImportCertificateToStoreByUserRequest(byte[] pfxBytes, string password, string userName, string storeName, bool privateKeyExportable)
    {
        PfxBytes = pfxBytes;
        Password = password;
        UserName = userName;
        StoreName = storeName;
        PrivateKeyExportable = privateKeyExportable;
    }

    public byte[] PfxBytes { get; set; }
    public string Password { get; set; }
    public string UserName { get; set; }
    public string StoreName { get; set; }
    public bool PrivateKeyExportable { get; set; }

    public VoidResponse DoIt(IWindowsX509CertificateStore certificateStore)
    {
        certificateStore.ImportCertificateToStore(PfxBytes, Password, UserName, StoreName, PrivateKeyExportable);
        return new VoidResponse();
    }
}

public class ImportCertificateToStoreByLocationRequest : IRequest
{
    public ImportCertificateToStoreByLocationRequest(byte[] pfxBytes, string password,  StoreLocation storeLocation,string storeName, bool privateKeyExportable)
    {
        PfxBytes = pfxBytes;
        Password = password;
        StoreLocation = storeLocation;
        StoreName = storeName;
        PrivateKeyExportable = privateKeyExportable;
    }

    public byte[] PfxBytes { get; set; }
    public string Password { get; set; }
    public StoreLocation StoreLocation { get; set; }
    public string StoreName { get; set; }
    public bool PrivateKeyExportable { get; set; }

    public VoidResponse DoIt(IWindowsX509CertificateStore certificateStore)
    {
        certificateStore.ImportCertificateToStore(PfxBytes, Password, StoreLocation, StoreName, PrivateKeyExportable);
        return new VoidResponse();
    }
}

public class AddPrivateKeyAccessRulesRequest : IRequest
{
    public AddPrivateKeyAccessRulesRequest(string thumbprint, StoreLocation storeLocation, string storeName, List<PrivateKeyAccessRule> privateKeyAccessRules)
    {
        this.Thumbprint = thumbprint;
        StoreName = storeName;
        PrivateKeyAccessRules = privateKeyAccessRules;
        StoreLocation = storeLocation;
    }

    public string Thumbprint { get; set; }
    public StoreLocation StoreLocation { get; set; }
    public string StoreName { get; set; }
    public List<PrivateKeyAccessRule> PrivateKeyAccessRules { get; set; }
    
    
    public VoidResponse DoIt(IWindowsX509CertificateStore certificateStore)
    {
        if (string.IsNullOrEmpty(StoreName))
        {
            certificateStore.AddPrivateKeyAccessRules(Thumbprint, StoreLocation, PrivateKeyAccessRules);    
        }
        else
        {
            certificateStore.AddPrivateKeyAccessRules(Thumbprint, StoreLocation, StoreName, PrivateKeyAccessRules);
        }

        return new VoidResponse();
    }
}

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