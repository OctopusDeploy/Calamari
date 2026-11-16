using Calamari.Build.Types;

namespace Calamari.Build;

public partial class Build
{
    const string JSignDefaultDockerImage = "docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-jsign-cli";
    [Parameter("JSign docker tool image to use. Defaults to " + JSignDefaultDockerImage)] public string JSignDockerImage = JSignDefaultDockerImage;

    [Parameter(Name = "signing_certificate_password"), Secret]
    readonly string SigningCertificatePassword = "Password01!";

    [Parameter] readonly string? AzureKeyVaultUrl;

    [Parameter] readonly string? AzureKeyVaultAppId;

    [Parameter] readonly string? AzureKeyVaultTenantId;

    [Parameter, Secret] readonly string? AzureKeyVaultAppSecret;

    [Parameter] readonly string? AzureKeyVaultCertificateName;

    AzureKeyVaultSettings AzureKeyVaultSettings => new(
        AzureKeyVaultUrl,
        AzureKeyVaultAppSecret,
        AzureKeyVaultAppId,
        AzureKeyVaultCertificateName,
        AzureKeyVaultTenantId);

    [Parameter(Name = "signing_certificate_path")]
    readonly string SigningCertificatePath = RootDirectory / "certificates" / "OctopusDevelopment.pfx"; 
}