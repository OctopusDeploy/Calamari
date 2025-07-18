BeforeAll {
    Remove-Module "azure" -Force -ErrorAction SilentlyContinue
    Import-Module (Join-Path $PSScriptRoot "helpers/azure.psm1")

    Remove-Module "cleanup" -Force -ErrorAction SilentlyContinue
    Import-Module (Join-Path $PSScriptRoot "helpers/cleanup.psm1")

    Remove-Module "logging" -Force -ErrorAction SilentlyContinue
    Import-Module (Join-Path $PSScriptRoot "helpers/logging.psm1")

    Remove-Module "octopus" -Force -ErrorAction SilentlyContinue
    Import-Module (Join-Path $PSScriptRoot "helpers/octopus.psm1")

    Remove-Module "slack" -Force -ErrorAction SilentlyContinue
    Import-Module (Join-Path $PSScriptRoot "helpers/slack.psm1")
}

Describe "Invoke-SandboxCleanup Tests" {
    It "Should make the correct cleanup decision" {
        $desiredDateFormat = "yyyy-MM-dd HH:mm"
        $date = [DateTime]::ParseExact((Get-Date).ToString($desiredDateFormat), $desiredDateFormat, $null)
        $mockedResponse = @{
            Value = @(
                @{
                    id = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/sandboxcleanup-tests-expired-withTag";
                    name = "sandboxcleanup-tests-expired-withTag";
                    type = "Microsoft.Resources/resourceGroups";
                    location = "eastus";
                    createdTime = $date.AddDays(-14).ToString($desiredDateFormat);
                    tags = @{ LifeTimeInDays = "7"; CleanupNotificationSent = "True" }
                },
                @{
                    id = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/sandboxcleanup-tests-notexpired-withTag";
                    name = "sandboxcleanup-tests-notexpired-withTag";
                    type = "Microsoft.Resources/resourceGroups";
                    location = "eastus";
                    createdTime = $date.AddDays(-3).ToString($desiredDateFormat);
                    tags = @{ LifeTimeInDays = "7" }
                },
                @{
                    id = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/sandboxcleanup-tests-expired-withoutTag";
                    name = "sandboxcleanup-tests-expired-withoutTag";
                    type = "Microsoft.Resources/resourceGroups";
                    location = "eastus";
                    createdTime = $date.AddDays(-4).ToString($desiredDateFormat);
                    tags = @{ CleanupNotificationSent = "True" }
                },
                @{
                    id = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/MC_sandboxcleanup-clustergroup";
                    name = "MC_sandboxcleanup-clustergroup";
                    type = "Microsoft.Resources/resourceGroups";
                    location = "eastus";
                    createdTime = $date.AddDays(0).ToString($desiredDateFormat);
                    tags = @{ }
                },
                @{
                    id = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/NetworkWatcherRG";
                    name = "NetworkWatcherRG";
                    type = "Microsoft.Resources/resourceGroups";
                    location = "eastus";
                    createdTime = $date.AddDays(-40).ToString($desiredDateFormat);
                    tags = @{ }
                },
                @{
                    id = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/survived-chopping-block";
                    name = "survived-chopping-block";
                    type = "Microsoft.Resources/resourceGroups";
                    location = "eastus";
                    createdTime = $date.AddDays(-2).ToString($desiredDateFormat);
                    tags = @{ LifeTimeInDays = "7"; CleanupNotificationSent = "True" }
                }
            )
        }

        Mock Invoke-RestMethod {
            $mockedResponse
        } -ModuleName azure

        $OctopusParameters = @{
            "SendSlackNotification" = "False"
            "WriteCleanupNotificationTag" = "False"
            "Octopus.Environment.Name" = "Azure Notify"
            "NoDeleteToleranceInHours" = 28
            "DeleteCadenceString" = "Friday at 9:00 AM Brisbane Time"
            "AzureAccount.Client" = "removed"
            "AzureAccount.Password" = $Env:AZURE_SERVICE_PRINCIPAL_SECRET
            "AzureAccount.TenantId" = "removed"
            "AzureAccount.SubscriptionNumber" = "removed"
            "AzureSubscriptionID" = "removed"
            "SlackBearerToken" = $Env:SLACK_BEARER_TOKEN
            "TeamSlackChannel" = "removed"
        }

        # Import the script containing the Invoke-SandboxCleanup function
        . "$PSScriptRoot/Invoke-SandboxCleanup.ps1"

        # Ensure the exclusions list exists in the working directory
        Copy-Item -Path "$PSScriptRoot/resourceGroupExclusionList.json" -Destination "./" -ErrorAction SilentlyContinue

        $output = Invoke-SandboxCleanup | Select-Object ResourceGroupName, Expired, CleanupAction | Where-Object {
            $null -ne $_.ResourceGroupName -and `
            $null -ne $_.Expired -and `
            $null -ne $_.CleanupAction
        }

        $output[0].ResourceGroupName | Should -Be "sandboxcleanup-tests-expired-withTag"
        $output[0].CleanupAction | Should -Be "Delete" -Because "sandboxcleanup-tests-expired-withTag is expired and must be deleted"

        $output[1].ResourceGroupName | Should -Be "sandboxcleanup-tests-notexpired-withTag"
        $output[1].CleanupAction | Should -Be "Ignore" -Because "sandboxcleanup-tests-notexpired-withTag is NOT expired and must be ignored"

        $output[2].ResourceGroupName | Should -Be "sandboxcleanup-tests-expired-withoutTag"
        $output[2].CleanupAction | Should -Be "Delete" -Because "sandboxcleanup-tests-expired-withoutTag is expired and must be deleted"

        $output[3].ResourceGroupName | Should -Be "MC_sandboxcleanup-clustergroup"
        $output[3].CleanupAction | Should -Be "Ignore" -Because "MC_sandboxcleanup-clustergroup managed by Azure and should be ignored"

        $output[4].ResourceGroupName | Should -Be "NetworkWatcherRG"
        $output[4].CleanupAction | Should -Be "Ignore"  -Because "NetworkWatcherRG is on the ignore list and should be ignored"

        $output[5].ResourceGroupName | Should -Be "survived-chopping-block"
        $output[5].CleanupAction | Should -Be "Ignore" -Because "survived-chopping-block was extended in lifetime and should be ignored"
    }
}