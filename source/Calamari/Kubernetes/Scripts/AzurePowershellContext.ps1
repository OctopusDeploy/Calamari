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

function Initialize-AzureRmContext
{
    # Turn off context autosave, as this will make all authentication occur in memory, and isolate each session from the context changes in other sessions
    Write-Host "##octopus[stdout-verbose]"
    Disable-AzureRMContextAutosave -Scope Process
    Write-Host "##octopus[stdout-default]"

    $AzureEnvironment = Get-AzureRmEnvironment -Name $OctopusAzureEnvironment
    if (!$AzureEnvironment)
    {
        Write-Error "No Azure environment could be matched given the name $OctopusAzureEnvironment"
        exit 2
    }

    If ([System.Convert]::ToBoolean($OctopusUseOidc)) {
            Write-Verbose "AzureRM Modules: Authenticating with OpenID Connect Federated Token"

            # Force any output generated to be verbose in Octopus logs.
            Write-Host "##octopus[stdout-verbose]"
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Login-AzureRmAccount -Environment $AzureEnvironment -ApplicationId $OctopusAzureADClientId -Tenant $OctopusAzureADTenantId -Subscription $OctopusAzureSubscriptionId -FederatedToken $OctopusOpenIdJwt
            Write-Host "##octopus[stdout-default]"
    }
    else {
        # Authenticate via Service Principal
        $securePassword = ConvertTo-SecureString $OctopusAzureADPassword -AsPlainText -Force
        $creds = New-Object System.Management.Automation.PSCredential ($OctopusAzureADClientId, $securePassword)

        Write-Verbose "AzureRM Modules: Authenticating with Service Principal"

        # Force any output generated to be verbose in Octopus logs.
        Write-Host "##octopus[stdout-verbose]"
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Login-AzureRmAccount -Credential $creds -TenantId $OctopusAzureADTenantId -SubscriptionId $OctopusAzureSubscriptionId -Environment $AzureEnvironment -ServicePrincipal
        Write-Host "##octopus[stdout-default]"
    }
}

function Initialize-AzContext
{
    $tempWarningPreference = $WarningPreference
    $WarningPreference = 'SilentlyContinue'
    if (-Not(Get-Command "Disable-AzureRMContextAutosave" -errorAction SilentlyContinue))
    {
        $WarningPreference = $tempWarningPreference
        Write-Verbose "Enabling AzureRM aliasing"

        # Turn on AzureRm aliasing
        # See https://docs.microsoft.com/en-us/powershell/azure/migrate-from-azurerm-to-az?view=azps-3.0.0#enable-azurerm-compatibility-aliases
        Enable-AzureRmAlias -Scope Process
    }
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
    # Depending on which version of Powershell we are running under will change which module context we want to initialize.
    #   Powershell Core: Check Az then AzureRM (provide a warning and do nothing if AzureRM is installed)
    #   Windows Powershell: Check AzureRM then Az
    #
    # If a module is installed (e.g. AzureRM) then testing for it causes the module to be loaded along with its various assemblies
    # and dependencies. If we then test for the other module (e.g. Az) then it also loads it's module and assemblies which then
    # creates havoc when you try and call any context methods such as Disable-AzContextAutosave due to version differences etc.
    # For this reason we'll only test the module we prefer and then if it exists initialize it and not the other one.
    if (Get-RunningInPowershellCore)
    {
        if (Get-AzModuleInstalled)
        {
            Initialize-AzContext
        }
        elseif (Get-AzureRmModuleInstalled)
        {
            # AzureRM is not supported on powershell core
            Write-Warning "AzureRM module is not compatible with Powershell Core, authentication will not be performed with AzureRM"
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
            if($OctopusAzureRmIsDeprecated -like [Boolean]::TrueString) {
                Write-Error "Azure Resource Manager modules are no longer available for authenticating with Azure, you are required to move to Azure CLI or the Az powershell modules."
                exit 2
            }
            else {
                Write-Warning "Azure Resource Manager powershell module has reached end-of-life; please authenticate using Azure CLI or the Az module, Octopus will prevent usage of the AzureRM module in 2024.3."
                Initialize-AzureRmContext
            }
        }
    }
}

Write-Host "##octopus[stdout-verbose]"
ConnectAzAccount

if ([string]::IsNullOrEmpty($OctopusKubernetesTargetScriptParameters)) {
    Write-Verbose "Invoking target script '$OctopusKubernetesTargetScript'."
} else {
    Write-Verbose "Invoking target script '$OctopusKubernetesTargetScript' with parameters '$OctopusKubernetesTargetScriptParameters'."
}
Write-Host "##octopus[stdout-default]"

Invoke-Expression ". `"$OctopusKubernetesTargetScript`" $OctopusKubernetesTargetScriptParameters"
