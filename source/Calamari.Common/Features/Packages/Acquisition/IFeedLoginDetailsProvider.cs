using System;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Packages.Acquisition;

public interface IFeedLoginDetailsProvider
{
    Task<(string Username, string Password, Uri FeedUri)> GetFeedLoginDetails(
        IVariables variables,
        string? username,
        string? password,
        Uri feedUri);
}