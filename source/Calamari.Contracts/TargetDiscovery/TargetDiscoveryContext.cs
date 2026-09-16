namespace Octopus.Calamari.Contracts.TargetDiscovery;

public interface ITargetDiscoveryContext
{
    public const string VariableName = TargetDiscoverySpecialVariables.TargetDiscoveryContext;

    public TargetDiscoveryScope Scope { get; }
}

public class TargetDiscoveryContext<TAuthenticationScope> : ITargetDiscoveryContext
    where TAuthenticationScope : ITargetDiscoveryAuthenticationDetails
{
    public TargetDiscoveryContext(TargetDiscoveryScope scope, TAuthenticationScope authentication)
    {
        Scope = scope;
        Authentication = authentication;
    }

    public TargetDiscoveryScope Scope { get; set; }

    public TAuthenticationScope Authentication { get; set; }
}