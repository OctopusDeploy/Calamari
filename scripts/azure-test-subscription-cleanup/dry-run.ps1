#/usr/bin/env pwsh

# Use this script to get an output of what will happen when run. Useful for testing.

$OctopusParameters = @{
  "SendSlackNotification"           = "False"
  "WriteCleanupNotificationTag"     = "False"
  "Octopus.Environment.Name"        = "Azure Notify"
  "NoDeleteToleranceInHours"        = 1
  "DeleteCadenceString"             = "Friday at 9:00 AM Brisbane Time"
  "AzureAccount.Client"             = $Env:AZURE_CLIENT_ID
  "AzureAccount.Password"           = $Env:AZURE_SERVICE_PRINCIPAL_SECRET
  "AzureAccount.TenantId"           = $Env:AZURE_TENANT_ID
  "AzureAccount.SubscriptionNumber" = $Env:AZURE_SUBSCRIPTION_ID
  "AzureSubscriptionID"             = $Env:AZURE_SUBSCRIPTION_ID
  "SlackBearerToken"                = $Env:SLACK_BEARER_TOKEN
  "TeamSlackChannel"                = ""
}

. ./scripts/azure-test-subscription-cleanup/Invoke-SandboxCleanup.ps1 

$output = Invoke-SandboxCleanup | Select-Object ResourceGroupName, Expired, CleanupAction | Where-Object {
  $null -ne $_.ResourceGroupName -and `
    $null -ne $_.Expired -and `
    $null -ne $_.CleanupAction
}

$output