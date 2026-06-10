
using Octopus.Calamari.Contracts.Attributes;

namespace Octopus.Calamari.Contracts.TargetDiscovery;

[method: JsonConstructor]
public class AccountAuthenticationDetails<TAccountDetails>(string type, string accountId, string authenticationMethod, TAccountDetails accountDetails)
    : ITargetDiscoveryAuthenticationDetails
{
    public string Type { get; set; } = type;

    public string AuthenticationMethod { get; } = authenticationMethod;

    public string AccountId { get; set; } = accountId;

    public TAccountDetails AccountDetails { get; set; } = accountDetails;
}