namespace Calamari.Build.Types;

record AzureKeyVaultSettings(
    string? AzureKeyVaultUrl,
    string? AzureKeyVaultAppSecret,
    string? AzureKeyVaultAppId,
    string? AzureKeyVaultCertificateName,
    string? AzureKeyVaultTenantId)
{
    public bool IsEmpty()
        => string.IsNullOrEmpty(AzureKeyVaultTenantId)
           && string.IsNullOrEmpty(AzureKeyVaultUrl)
           && string.IsNullOrEmpty(AzureKeyVaultAppId)
           && string.IsNullOrEmpty(AzureKeyVaultAppSecret)
           && string.IsNullOrEmpty(AzureKeyVaultCertificateName);
}