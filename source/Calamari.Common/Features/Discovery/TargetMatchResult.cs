using System;
using System.Collections.Generic;

namespace Calamari.Common.Features.Discovery;

public class TargetMatchResult
{
    readonly string? role;

    TargetMatchResult(string role, string? tenantedDeploymentMode = null)
    {
        this.role = role;
        TenantedDeploymentMode = tenantedDeploymentMode;
        FailureReasons = [];
    }

    TargetMatchResult(IEnumerable<string> failureReasons)
    {
        FailureReasons = failureReasons;
    }

    public string Role => role ?? throw new InvalidOperationException("Cannot get Role from failed target match result.");
    public string? TenantedDeploymentMode { get; }

    public IEnumerable<string> FailureReasons { get; }

    public bool IsSuccess => role != null;

    public static TargetMatchResult Success(string role, string? tenantedDeploymentMode) => new(role, tenantedDeploymentMode);

    public static TargetMatchResult Failure(IEnumerable<string> failureReasons) => new(failureReasons);
}