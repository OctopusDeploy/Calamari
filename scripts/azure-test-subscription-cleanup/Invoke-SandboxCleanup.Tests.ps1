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
                # Original sandbox tests
                @{
                    id          = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/sandboxcleanup-tests-expired-withLifetimeTag";
                    name        = "sandboxcleanup-tests-expired-withLifetimeTag";
                    type        = "Microsoft.Resources/resourceGroups";
                    location    = "eastus";
                    createdTime = $date.AddDays(-14).ToString($desiredDateFormat);
                    tags        = @{ LifeTimeInDays = "7"; CleanupNotificationSent = "True" }
                },
                @{
                    id          = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/sandboxcleanup-tests-notexpired-withLifetimeTag";
                    name        = "sandboxcleanup-tests-notexpired-withLifetimeTag";
                    type        = "Microsoft.Resources/resourceGroups";
                    location    = "eastus";
                    createdTime = $date.AddDays(-3).ToString($desiredDateFormat);
                    tags        = @{ LifeTimeInDays = "7" }
                },
                @{
                    id          = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/sandboxcleanup-tests-expired-withoutLifetimeTag";
                    name        = "sandboxcleanup-tests-expired-withoutLifetimeTag";
                    type        = "Microsoft.Resources/resourceGroups";
                    location    = "eastus";
                    createdTime = $date.AddDays(-4).ToString($desiredDateFormat);
                    tags        = @{ CleanupNotificationSent = "True" }
                },
                @{
                    id          = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/MC_sandboxcleanup-clustergroup";
                    name        = "MC_sandboxcleanup-clustergroup";
                    type        = "Microsoft.Resources/resourceGroups";
                    location    = "eastus";
                    createdTime = $date.AddDays(0).ToString($desiredDateFormat);
                    tags        = @{ }
                },
                @{
                    id          = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/NetworkWatcherRG";
                    name        = "NetworkWatcherRG";
                    type        = "Microsoft.Resources/resourceGroups";
                    location    = "eastus";
                    createdTime = $date.AddDays(-40).ToString($desiredDateFormat);
                    tags        = @{ }
                },
                @{
                    id          = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/survived-chopping-block";
                    name        = "survived-chopping-block";
                    type        = "Microsoft.Resources/resourceGroups";
                    location    = "eastus";
                    createdTime = $date.AddDays(-2).ToString($desiredDateFormat);
                    tags        = @{ LifeTimeInDays = "7"; CleanupNotificationSent = "True" }
                },
                # Calamari specific tests
                @{
                    id          = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/sandboxcleanup-tests-expired-withLifetimeTagAndCalamariSourceTag";
                    name        = "sandboxcleanup-tests-expired-withLifetimeTagAndCalamariSourceTag";
                    type        = "Microsoft.Resources/resourceGroups";
                    location    = "eastus";
                    createdTime = $date.AddDays(-14).ToString($desiredDateFormat);
                    tags        = @{ LifeTimeInDays = "7"; source = "calamari-e2e-tests" }
                },
                @{
                    id          = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/sandboxcleanup-tests-expired-withLifetimeTagAndOtherSourceTag";
                    name        = "sandboxcleanup-tests-expired-withLifetimeTagAndOtherSourceTag";
                    type        = "Microsoft.Resources/resourceGroups";
                    location    = "eastus";
                    createdTime = $date.AddDays(-14).ToString($desiredDateFormat);
                    tags        = @{ LifeTimeInDays = "7"; source = "something-else" }
                },
                @{
                    id          = "/subscriptions/00000000-0000-0000-0000-00000000000/resourceGroups/sandboxcleanup-tests-notexpired-withLifetimeTagAndCalamariSourceTag";
                    name        = "sandboxcleanup-tests-notexpired-withLifetimeTagAndCalamariSourceTag";
                    type        = "Microsoft.Resources/resourceGroups";
                    location    = "eastus";
                    createdTime = $date.AddDays(-3).ToString($desiredDateFormat);
                    tags        = @{ LifeTimeInDays = "7"; source = "calamari-e2e-tests" }
                }
            )
        }

        Mock Invoke-RestMethod {
            $mockedResponse
        } -ModuleName azure

        $OctopusParameters = @{
            "SendSlackNotification"           = "False"
            "WriteCleanupNotificationTag"     = "False"
            "Octopus.Environment.Name"        = "Azure Notify"
            "NoDeleteToleranceInHours"        = 1
            "DeleteCadenceString"             = "Friday at 9:00 AM Brisbane Time"

            # These are required to be present but not valid as the test mocks Azure
            "AzureAccount.Client"             = '$Env:AZURE_CLIENT_ID'
            "AzureAccount.Password"           = '$Env:AZURE_SERVICE_PRINCIPAL_SECRET'
            "AzureAccount.TenantId"           = '$Env:AZURE_TENANT_ID'
            "AzureAccount.SubscriptionNumber" = '$Env:AZURE_SUBSCRIPTION_ID'
            "AzureSubscriptionID"             = '$Env:AZURE_SUBSCRIPTION_ID'
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

        $output[0].ResourceGroupName | Should -Be "sandboxcleanup-tests-expired-withLifetimeTag"
        $output[0].CleanupAction | Should -Be "Ignore" -Because "sandboxcleanup-tests-expired-withTag does not have calamari source tag and must be ignored"

        $output[1].ResourceGroupName | Should -Be "sandboxcleanup-tests-notexpired-withLifetimeTag"
        $output[1].CleanupAction | Should -Be "Ignore" -Because "sandboxcleanup-tests-notexpired-withTag is NOT expired and must be ignored"

        $output[2].ResourceGroupName | Should -Be "sandboxcleanup-tests-expired-withoutLifetimeTag"
        $output[2].CleanupAction | Should -Be "Ignore" -Because "sandboxcleanup-tests-expired-withoutTag does not have calamari source tag and must be ignored"

        $output[3].ResourceGroupName | Should -Be "MC_sandboxcleanup-clustergroup"
        $output[3].CleanupAction | Should -Be "Ignore" -Because "MC_sandboxcleanup-clustergroup managed by Azure and should be ignored"

        $output[4].ResourceGroupName | Should -Be "NetworkWatcherRG"
        $output[4].CleanupAction | Should -Be "Ignore"  -Because "NetworkWatcherRG is on the ignore list and should be ignored"

        $output[5].ResourceGroupName | Should -Be "survived-chopping-block"
        $output[5].CleanupAction | Should -Be "Ignore" -Because "survived-chopping-block was extended in lifetime and should be ignored"

        $output[6].ResourceGroupName | Should -Be "sandboxcleanup-tests-expired-withLifetimeTagAndCalamariSourceTag"
        $output[6].CleanupAction | Should -Be "Delete" -Because "sandboxcleanup-tests-expired-withLifetimeTagAndCalamariSourceTag is expired and must be deleted"

        $output[7].ResourceGroupName | Should -Be "sandboxcleanup-tests-expired-withLifetimeTagAndOtherSourceTag"
        $output[7].CleanupAction | Should -Be "Ignore" -Because "sandboxcleanup-tests-expired-withLifetimeTagAndOtherSourceTag does not have calamari source tag and must be ignored"

        $output[8].ResourceGroupName | Should -Be "sandboxcleanup-tests-notexpired-withLifetimeTagAndCalamariSourceTag"
        $output[8].CleanupAction | Should -Be "Ignore" -Because "sandboxcleanup-tests-notexpired-withLifetimeTagAndCalamariSourceTag is NOT expired and must be ignored"
    }
}