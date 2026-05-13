using System;

namespace Octopus.Calamari.Contracts.ArgoCD;

public record ArgoCDCustomPropertiesDto(
    ArgoCDGatewayDto[] Gateways,
    ArgoCDApplicationDto[] Applications,
    IGitCredentialDto[] Credentials
);

public record ArgoCDGatewayDto(string Id, string Name);

public record ArgoCDApplicationDto(
    string GatewayId,
    string Name,
    string KubernetesNamespace,
    string Manifest,
    string DefaultRegistry,
    string? InstanceWebUiUrl);

public interface IGitCredentialDto
{
    string Type { get; }
    string Url { get; }
}

// UsernamePasswordGitCredentialDto - could rename, but not worth altering the API
public record GitCredentialDto(string Url, string Username, string Password) : IGitCredentialDto
{
    public const string DiscriminatorValue = "UsernamePassword";
    public string Type => DiscriminatorValue;
}

public record SshKeyGitCredentialDto(string Url, string Username, string PrivateKey) : IGitCredentialDto
{
    public const string DiscriminatorValue = "SshKey";
    public string Type => DiscriminatorValue;
}
