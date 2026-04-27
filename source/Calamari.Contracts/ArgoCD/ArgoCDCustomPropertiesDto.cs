using System;

namespace Octopus.Calamari.Contracts.ArgoCD;

public record ArgoCDCustomPropertiesDto(
    ArgoCDGatewayDto[] Gateways,
    ArgoCDApplicationDto[] Applications,
    GitCredentialDto[] Credentials,
    // Nullable for backwards compatibility
    GitCredentialSshKeyDto[]? SshCredentials);

public record ArgoCDGatewayDto(string Id, string Name);

public record ArgoCDApplicationDto(
    string GatewayId,
    string Name,
    string KubernetesNamespace,
    string Manifest,
    string DefaultRegistry,
    string? InstanceWebUiUrl);

// GitUsernamePasswordCredentialDto
public record GitCredentialDto(string Url, string Username, string Password);

public record GitCredentialSshKeyDto(string Url, string Username, string PrivateKey, string PublicKey, string? Passphrase);