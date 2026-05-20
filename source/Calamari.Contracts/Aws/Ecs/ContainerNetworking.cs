namespace Octopus.Calamari.Contracts.Aws.Ecs;

public record ContainerNetworkSettings
{
    public string DisableNetworking { get; init; } = string.Empty;
    public List<string> DnsServers { get; init; } = [];
    public List<string> DnsSearchDomains { get; init; } = [];
    public List<ExtraHost> ExtraHosts { get; init; } = [];
}

public record ExtraHost
{
    public string Hostname { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
}
