using Newtonsoft.Json;
using Octopus.Calamari.Contracts.TargetDiscovery;

namespace Calamari.Common.Features.Discovery;

/// <summary>
/// For account-based authentication scopes.
/// </summary>
[method: JsonConstructor]
public class AccountAuthenticationDetails<TAccountDetails>(string type, string accountId, string authenticationMethod, TAccountDetails accountDetails)
    : ITargetDiscoveryAuthenticationDetails
{
    public string Type { get; set; } = type;

    public string AuthenticationMethod { get; } = authenticationMethod;

    public string AccountId { get; set; } = accountId;

    public TAccountDetails AccountDetails { get; set; } = accountDetails;
}