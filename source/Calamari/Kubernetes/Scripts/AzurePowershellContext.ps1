## Octopus Kubernetes Context script
## --------------------------------------------------------------------------------------
##
## This script is used to configure the default azure context for Azure PS modules.

$OctopusAzureSubscriptionId = $OctopusParameters["Octopus.Action.Azure.SubscriptionId"]
$OctopusAzureADTenantId = $OctopusParameters["Octopus.Action.Azure.TenantId"]
$OctopusAzureADClientId = $OctopusParameters["Octopus.Action.Azure.ClientId"]
$OctopusAzureADPassword = $OctopusParameters["Octopus.Action.Azure.Password"]
$OctopusAzureEnvironment = $OctopusParameters["Octopus.Action.Azure.Environment"]
$OctopusOpenIdJwt = $OctopusParameters["Octopus.OpenIdConnect.Jwt"]
$OctopusUseOidc = ![string]::IsNullOrEmpty($OctopusOpenIdJwt)

if ($null -eq $OctopusAzureEnvironment)
{
    $OctopusAzureEnvironment = "AzureCloud"
}

function EnsureDirectoryExists([string] $path)
{
    New-Item -ItemType Directory -Force -Path $path *> $null
}

function Get-AzureRmModuleInstalled
{
    return $null -ne (Get-Command "Login-AzureRmAccount" -ErrorAction SilentlyContinue)
}

function Get-AzModuleInstalled
{
    return $null -ne (Get-Command "Connect-AzAccount" -ErrorAction SilentlyContinue)
}

function Get-RunningInPowershellCore
{
    return $PSVersionTable.PSVersion.Major -gt 5
}

function Initialize-AzContext
{
    $tempWarningPreference = $WarningPreference
    $WarningPreference = 'SilentlyContinue'
    $WarningPreference = $tempWarningPreference

    # Turn off context autosave, as this will make all authentication occur in memory, and isolate each session from the context changes in other sessions
    Write-Host "##octopus[stdout-verbose]"
    Disable-AzContextAutosave -Scope Process
    Write-Host "##octopus[stdout-default]"

    $AzureEnvironment = Get-AzEnvironment -Name $OctopusAzureEnvironment
    if (!$AzureEnvironment)
    {
        Write-Error "No Azure environment could be matched given the name $OctopusAzureEnvironment"
        exit 2
    }

    If ([System.Convert]::ToBoolean($OctopusUseOidc)) {
        Write-Verbose "Az Modules: Authenticating with OpenID Connect FederatedToken"
        # Force any output generated to be verbose in Octopus logs.
        Write-Host "##octopus[stdout-verbose]"
        Connect-AzAccount -Environment $AzureEnvironment -ApplicationId $OctopusAzureADClientId -Tenant $OctopusAzureADTenantId -Subscription $OctopusAzureSubscriptionId -FederatedToken $OctopusOpenIdJwt
        Write-Host "##octopus[stdout-default]"
    }
    else {
        # Authenticate via Service Principal
        $securePassword = ConvertTo-SecureString $OctopusAzureADPassword -AsPlainText -Force
        $creds = New-Object System.Management.Automation.PSCredential ($OctopusAzureADClientId, $securePassword)

        Write-Verbose "Az Modules: Authenticating with Service Principal"

        # Force any output generated to be verbose in Octopus logs.
        Write-Host "##octopus[stdout-verbose]"
        Connect-AzAccount -Credential $creds -TenantId $OctopusAzureADTenantId -SubscriptionId $OctopusAzureSubscriptionId -Environment $AzureEnvironment -ServicePrincipal
        Write-Host "##octopus[stdout-default]"
    }
}

function ConnectAzAccount
{
    # Since AzureRM module is deprecated and no longer recommended for use in Powershell
    # We check for the up to date Az module first, if that isn't available we check for the AzureRM module then warn the user and then do nothing.
    if (Get-RunningInPowershellCore)
    {
        if (Get-AzModuleInstalled)
        {
            Initialize-AzContext
        }
        elseif (Get-AzureRmModuleInstalled)
        {
            # AzureRM has been deprecated since 2024-02-29 https://learn.microsoft.com/en-us/powershell/azure/azurerm-retirement-overview
            Write-Warning "AzureRM module is deprecated since 2024-02-29, Az module is now required to authenticate with Azure. See https://learn.microsoft.com/en-us/powershell/azure/azurerm-retirement-overview for more details."
        }
    }
    else
    {
        # Windows Powershell
        if (Get-AzModuleInstalled)
        {
            Initialize-AzContext
        }
        elseif (Get-AzureRmModuleInstalled)
        {
            # AzureRM has been deprecated since 2024-02-29 https://learn.microsoft.com/en-us/powershell/azure/azurerm-retirement-overview
            Write-Warning "AzureRM module is deprecated since 2024-02-29, Az module is now required to authenticate with Azure. See https://learn.microsoft.com/en-us/powershell/azure/azurerm-retirement-overview for more details."
        }
    }
}

Write-Host "##octopus[stdout-verbose]"
ConnectAzAccount

Write-Verbose "Invoking target script $OctopusKubernetesTargetScript with $OctopusKubernetesTargetScriptParameters parameters"
Write-Host "##octopus[stdout-default]"

Invoke-Expression ". `"$OctopusKubernetesTargetScript`" $OctopusKubernetesTargetScriptParameters"