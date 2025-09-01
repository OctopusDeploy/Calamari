#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Wrapper script for Invoke-SandboxCleanup that takes individual parameters instead of relying on $OctopusParameters.

.DESCRIPTION
    This wrapper allows you to call Invoke-SandboxCleanup with explicit parameters rather than environment variables or Octopus parameters.

.PARAMETER ApplicationId
    Azure Service Principal Application ID

.PARAMETER ClientSecret
    Azure Service Principal Client Secret

.PARAMETER TenantId
    Azure Tenant ID

.PARAMETER SubscriptionId
    Azure Subscription ID

.PARAMETER EnvironmentName
    Environment name (use names containing "Cleanup" for actual deletion, otherwise runs in notify mode)

.PARAMETER SendSlackNotification
    Whether to send Slack notifications (default: False)

.PARAMETER WriteCleanupNotificationTag
    Whether to write cleanup notification tags (default: False)

.PARAMETER SlackBearerToken
    Slack bearer token for notifications (optional)

.PARAMETER TeamSlackChannel
    Slack channel for notifications (optional)

.PARAMETER NoDeleteToleranceInHours
    Hours to wait before deletion after notification (default: 1)

.PARAMETER DeleteCadenceString
    Human-readable description of deletion schedule (default: "Friday at 9:00 AM Brisbane Time")

.EXAMPLE
    ./Invoke-SandboxCleanupWrapper.ps1 -ApplicationId "your-app-id" -ClientSecret "your-secret" -TenantId "your-tenant" -SubscriptionId "your-subscription" -EnvironmentName "Azure Notify"

.EXAMPLE
    ./Invoke-SandboxCleanupWrapper.ps1 -ApplicationId $env:AZURE_CLIENT_ID -ClientSecret $env:AZURE_SERVICE_PRINCIPAL_SECRET -TenantId $env:AZURE_TENANT_ID -SubscriptionId $env:AZURE_SUBSCRIPTION_ID -EnvironmentName "Azure Cleanup"
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ApplicationId,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ClientSecret,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$TenantId,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$EnvironmentName,

    [Parameter(Mandatory = $false)]
    [bool]$SendSlackNotification = $false,

    [Parameter(Mandatory = $false)]
    [bool]$WriteCleanupNotificationTag = $false,

    [Parameter(Mandatory = $false)]
    [string]$SlackBearerToken = "",

    [Parameter(Mandatory = $false)]
    [string]$TeamSlackChannel = "",

    [Parameter(Mandatory = $false)]
    [int]$NoDeleteToleranceInHours = 1,

    [Parameter(Mandatory = $false)]
    [string]$DeleteCadenceString = "Friday at 9:00 AM Brisbane Time"
)

# Set up the global $OctopusParameters hashtable that Invoke-SandboxCleanup expects
$global:OctopusParameters = @{
    "AzureAccount.Client"             = $ApplicationId
    "AzureAccount.Password"           = $ClientSecret
    "AzureAccount.TenantId"           = $TenantId
    "AzureAccount.SubscriptionNumber" = $SubscriptionId
    "AzureSubscriptionID"             = $SubscriptionId
    "Octopus.Environment.Name"        = $EnvironmentName
    "SendSlackNotification"           = $SendSlackNotification.ToString()
    "WriteCleanupNotificationTag"     = $WriteCleanupNotificationTag.ToString()
    "SlackBearerToken"                = $SlackBearerToken
    "TeamSlackChannel"                = $TeamSlackChannel
    "NoDeleteToleranceInHours"        = $NoDeleteToleranceInHours
    "DeleteCadenceString"             = $DeleteCadenceString
}

# Source the main cleanup script
. "$PSScriptRoot/Invoke-SandboxCleanup.ps1"

# Execute the cleanup function
Write-Host "Starting Azure resource cleanup with the following parameters:"
Write-Host "  Application ID: $ApplicationId"
Write-Host "  Tenant ID: $TenantId"
Write-Host "  Subscription ID: $SubscriptionId"
Write-Host "  Environment: $EnvironmentName"
Write-Host "  Send Slack Notifications: $SendSlackNotification"
Write-Host ""

$result = Invoke-SandboxCleanup

# Return the result
return $result