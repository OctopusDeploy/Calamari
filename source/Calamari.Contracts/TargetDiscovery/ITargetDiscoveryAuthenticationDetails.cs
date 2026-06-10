namespace Octopus.Calamari.Contracts.TargetDiscovery;

public interface ITargetDiscoveryAuthenticationDetails
{
    string Type { get; }

    string? AuthenticationMethod { get; }
}