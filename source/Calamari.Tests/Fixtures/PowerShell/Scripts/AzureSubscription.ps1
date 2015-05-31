$subscription = Get-AzureSubscription -Current
Write-Host "Current subscription ID: $($subscription.SubscriptionId)"