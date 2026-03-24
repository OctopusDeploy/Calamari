#nullable enable

namespace Calamari.ArgoCD.Dtos;

public record ArgoCDCustomPropertiesDto(ArgoCDGatewayDto[] Gateways, ArgoCDApplicationDto[] Applications, GitCredentialDto[] Credentials, SshGitCredentialDto[]? SshCredentials = null);

public record ArgoCDGatewayDto(string Id, string Name);

public record ArgoCDApplicationDto(
    string GatewayId,
    string Name,
    string KubernetesNamespace,
    string Manifest,
    string DefaultRegistry,
    string? InstanceWebUiUrl);

public record GitCredentialDto(string Url, string Username, string Password);

public record SshGitCredentialDto(string Url, string Username, string PrivateKey, string PublicKey, string Passphrase)
{
    public override string ToString() => $"SshGitCredentialDto {{ Url = {Url}, Username = {Username} }}";
}
