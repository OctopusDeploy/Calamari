Function Show-OctopusArtifactDocument {
    param(
        [array]$ResourceGroups,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $AzureProperties
    )

    $resourceGroups | ConvertTo-Json > "./sandbox-cleanup-$($AzureProperties["SubscriptionId"]).json"
    New-OctopusArtifact "./sandbox-cleanup-$($AzureProperties["SubscriptionId"]).json"
}
