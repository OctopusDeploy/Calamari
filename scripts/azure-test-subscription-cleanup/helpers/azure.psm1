Function Get-StandardizeDateFormat {
    param([string]$dateString)

    # Define an array of possible date formats
    $dateFormats = @(
        "M/d/yyyy H:mm:ss", "d/M/yyyy H:mm:ss", # Formats with single-digit month/day
        "MM/dd/yyyy H:mm:ss", "dd/MM/yyyy H:mm:ss", # Formats with double-digit month/day
        "yyyy-MM-dd HH:mm:ss" # ISO format
    )

    foreach ($format in $dateFormats) {
        try {
            $parsedDate = [DateTime]::ParseExact($dateString, $format, [System.Globalization.CultureInfo]::InvariantCulture)
            return $parsedDate.ToString("yyyy-MM-dd HH:mm")
        }
        catch {
            continue
        }
    }

    return $dateString
}

Function New-AzureConnection {
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$ApplicationId,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [securestring]$ApplicationSecret,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$TenantId,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$SubscriptionId
    )

    $ApplicationCredential = New-Object System.Management.Automation.PSCredential($ApplicationId, $ApplicationSecret)
    Connect-AzAccount -ServicePrincipal -Credential $ApplicationCredential -TenantId $TenantId -Subscription $SubscriptionId | Out-Null
    Set-AzContext -Subscription $SubscriptionId -TenantId $TenantId | Out-Null

    # Something would be seriously wrong if we ran this tool in a non-sandbox subscription for some reason
    $actualSubscriptionContext = Get-AzContext
    if (-not($($actualSubscriptionContext.Subscription.Name) -like "*Sandbox")) {
        Write-Error "The Subscription Name of '$($actualSubscriptionContext.Subscription.Name)' does not contain the word 'Sandbox'. This tool is only meant for Sandbox Subscriptions "
        break;
    }

    return $($actualSubscriptionContext.Subscription.Name)
}

Function Get-SandboxResourceGroups {
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$SubscriptionId
    )

    $header = @{
        'Accept'        = 'application/json';
        'Authorization' = "Bearer $((Get-AzAccessToken -AsSecureString).Token | ConvertFrom-SecureString -AsPlainText)";
        'Content-Type'  = 'application/json'
    }

    Write-Verbose "Invoke web request to Azure API to retrieve resource groups from subscription with id '$SubscriptionId'"
    $result = (Invoke-RestMethod -Method "GET" -Uri "https://management.azure.com/subscriptions/$SubscriptionId/resourcegroups?api-version=2019-08-01&%24expand=createdTime" -Headers $header).Value

    $sandboxResourceGroups = $result | Select-Object `
    @{ expression = { $_.name }; label = 'ResourceGroupName' }, `
    @{ expression = { Get-StandardizeDateFormat $_.createdTime }; label = 'CreatedTime' }, `
    @{ expression = { $_.id }; label = 'ResourceId' }, `
    @{ expression = { $_.tags.LifetimeInDays }; label = 'LifetimeInDays' }, `
    @{ expression = { if ($_.tags.CleanupNotificationSent -eq "True") { $true } else { $false } }; label = 'CleanupNotificationIsSent' }

    return $sandboxResourceGroups
}

Function Add-CleanupProperties {
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $ResourceGroups,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $OctopusProperties,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $CleanupProperties
    )



    # Don't delete a resource group if it was only created within the last '$NoDeletetoleranceInHours' hours before this script runs.
    # This prevents resource groups that were only just created minutes/hours before the tool runs from being deleted.
    # this also prevents surprises where we've sent an alert on Thursday for x resource groups to be deleted, but were created after the initial notification period.
    $NoDeletetoleranceInHours = $cleanupProperties["NoDeleteToleranceInHours"]

    if ($($OctopusProperties["EnvironmentName"]).Contains("Notify")) {
        $NoDeletetoleranceInHours = 0
    }

    $date = [DateTime]::ParseExact((Get-Date).ToString($cleanupProperties["DesiredDateFormat"]), $cleanupProperties["DesiredDateFormat"], $null)

    $ResourceGroups | Add-Member NoteProperty ExpirationDate $null
    $ResourceGroups | Add-Member NoteProperty Expired $false
    $ResourceGroups | Add-Member NoteProperty CleanupAction $null
    $ResourceGroups | Add-Member NoteProperty CleanupActionReason $null
    
    $ResourceGroups | ForEach-Object {
        $LifetimeInDays = $_.LifetimeInDays
        $CreatedTime = [DateTime]::ParseExact($_.CreatedTime, $cleanupProperties["DesiredDateFormat"], $null)
        $Expired = $false

        if ($null -ne $LifetimeInDays) {
            $ExpirationDate = $CreatedTime.AddDays($LifetimeInDays).ToString($cleanupProperties["DesiredDateFormat"])
            $Expired = $date -ge $ExpirationDate
        }
        else {
            $ExpirationDate = $CreatedTime.AddHours($NoDeletetoleranceInHours).ToString($cleanupProperties["DesiredDateFormat"])
            $Expired = $date -gt $ExpirationDate
        }

        $_.ExpirationDate = $ExpirationDate
        $_.Expired = $Expired
    }

    $ResourceGroups | ForEach-Object {
        $cleanupResult = (Get-CleanupAction -ResourceGroupName $($_.ResourceGroupName) -IsExpired $($_.Expired))
        $_.CleanupAction = $cleanupResult[0]
        $_.CleanupActionReason = $cleanupResult[1]
    }

    return $ResourceGroups
}

Function Get-CleanupAction {
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $ResourceGroupName,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $IsExpired
    )

    $inputResourceGroupExclusionList = "./resourceGroupExclusionList.json"
    $resourceGroupExclusionList = Get-Content $((Get-Item $inputResourceGroupExclusionList).FullName) | ConvertFrom-Json

    if ($ResourceGroupName.StartsWith("MC_") ) {
        return @("Ignore", "Resource Group Name starts with 'MC_' which is a Microsoft Managed Cluster and the lifecycle is handled by Azure")
    }

    if ($ResourceGroupName -in $resourceGroupExclusionList) {
        return @("Ignore", "Resource Group Name exactly matches an entry on the resourceGroupExclusionList.json file")
    }

    if ($isExpired) {
        return @("Delete", "Resource Group is Expired")
    }

    return @("Ignore", "Resource Group did not match any criteria to delete")
}

Function Confirm-CleanupNotificationIsSentTag {
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $ResourceGroup
    )

    if ($ResourceGroup.CleanupNotificationIsSent -eq "True") { return $true } else { return $false }
}

Function Set-CleanupNotificationIsSentTag {
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $ResourceGroup
    )

    $notificationTags = @{
        "CleanupNotificationSent" = "True"
    }

    $rg = Get-AzResourceGroup -Id $ResourceGroup.ResourceId -ErrorAction SilentlyContinue

    if ($null -ne $rg) {
        Update-AzTag -ResourceId $ResourceGroup.ResourceId -Tag $notificationTags -Operation Merge | Out-Null
    }
}

Function Clear-CleanupNotificationIsSentTag {
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $ResourceGroup
    )

    $notificationTags = @{
        "CleanupNotificationSent" = "True"
    }

    $rg = Get-AzResourceGroup -Id $ResourceGroup.ResourceId -ErrorAction SilentlyContinue

    if ($null -ne $rg) {
        Update-AzTag -ResourceId $ResourceGroup.ResourceId -Tag $notificationTags -Operation Delete | Out-Null
    }
}

Function Get-SandboxResourceForSubscription {
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $AzureProperties,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $CleanupProperties,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $OctopusProperties
    )

    $connectedSubscriptionName = New-AzureConnection -ApplicationId $AzureProperties["ApplicationId"] `
        -ApplicationSecret (ConvertTo-SecureString $AzureProperties["ClientSecret"] -AsPlainText -Force) `
        -TenantId "removed" `
        -SubscriptionId $AzureProperties["SubscriptionId"]

    $groups = Get-SandboxResourceGroups -SubscriptionId $AzureProperties["SubscriptionId"]
    $groups | Add-Member NoteProperty ConnectedSubscriptionName $connectedSubscriptionName

    # Update the collection with an action to take
    if ($null -ne $groups) {
        $ResourceGroups = Add-CleanupProperties -ResourceGroups $groups `
            -CleanupProperties $CleanupProperties `
            -OctopusProperties $OctopusProperties
    }

    return $ResourceGroups
}

Function Start-ResourceGroupDeletion {
    param (
        [array]$ResourceGroupsToDelete,
        [string]$OctopusEnvironment,
        [string]$ConnectedSubscriptionName,
        $SlackProperties
    )

    if ($OctopusEnvironment.Contains("Cleanup")) {
        Write-Host "Running in the '$OctopusEnvironment' Environment, starting process to delete resource groups"
        $ResourceGroupsToDelete | ForEach-Object {
            Write-Host "Deleting Resource Group '$($_.ResourceGroupName)' in subscription '$ConnectedSubscriptionName'"
            
            try {
                $cleanupNotificationIsSent = Confirm-CleanupNotificationIsSentTag -ResourceGroup $_

                if ($cleanupNotificationIsSent) {
                    Remove-AzResourceGroup -Name $($_.ResourceGroupName) -Force -ErrorAction Stop  
                }
            }
            catch {
                $slackAttachmentTemplate = New-Object PSObject
                Add-Member -InputObject $slackAttachmentTemplate -MemberType NoteProperty -Name pretext -Value ""
                Add-Member -InputObject $slackAttachmentTemplate -MemberType NoteProperty -Name fallback -Value ""
                Add-Member -InputObject $slackAttachmentTemplate -MemberType NoteProperty -Name color -Value ""
                Add-Member -InputObject $slackAttachmentTemplate -MemberType NoteProperty -Name fields -Value @()

                $slackAttachment = $slackAttachmentTemplate
                $slackAttachment.pretext = ":Exclamation: Failed to delete $($_.ResourceGroupName) Azure Sandbox Resource groups in '$ConnectedSubscriptionName'"
                $slackAttachment.fallback = "Azure Sandbox Cleanup failed to delete resource group '$($_.ResourceGroupName)'"
                $slackAttachment.color = "#C21807"
                $slackAttachment.fields = @(
                    @{
                        value = "`````` `n $($_.Exception.Message) `n ``````"
                    }
                );               
                Invoke-NewSlackNotificationRequest -Attachments $slackAttachment -SlackProperties $slackProperties
            }
        }
    } 
    else {
        Write-Host "This script is running in an '$OctopusEnvironment' environment so cleanup is not going to occur. Cleanup Can only run when the Environment name is 'Azure Cleanup'"
    }
}

Function Invoke-CleanupSurvivingResourceGroups {
    param (
        [array]$ResourceGroupsSurvivedChoppingBlock,
        [string]$OctopusEnvironment,
        [string]$ConnectedSubscriptionName
    )

    if ($OctopusEnvironment.Contains("Cleanup")) {
        Write-host "Cleaing the NotificationIsSentTag on resource groups that are no longer expired"
        $ResourceGroupsSurvivedChoppingBlock | ForEach-Object {
            Write-Host "Resource group with the name '$($_.ResourceGroupName)' in subscription '$ConnectedSubscriptionName' survived the chopping block"
            Clear-CleanupNotificationIsSentTag -ResourceGroup $_
        }
    }
}