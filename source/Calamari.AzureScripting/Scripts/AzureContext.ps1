## Octopus Azure Context script, version 1.0
## --------------------------------------------------------------------------------------
##
## This script is used to load the Azure Powershell module and select the Azure subscription
##
## The script is passed the following parameters.
##
##   $OctopusAzureTargetScript = "..."
##   $OctopusAzureTargetScriptParameters = "..."
##   $OctopusAzureSubscriptionId = "..."
##   $OctopusAzureStorageAccountName = "..."
##   $OctopusAzureCertificateFileName = "..."
##   $OctopusAzureCertificatePassword = "..."
##   $OctopusAzureADTenantId = "..."
##   $OctopusAzureADClientId = "..."
##   $OctopusAzureADPassword = "..."
##   $OctopusAzureEnvironment = "..."
##   $OctopusDisableAzureCLI = "..."
##   $OctopusAzureExtensionsDirectory = "..." 
##   OctopusOpenIdJwt = "..."

$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -lt 5)
{
    throw "These Azure commands are only supported in PowerShell versions 5 and above. This server is currently running PowerShell version $($PSVersionTable.PSVersion.ToString())."
}

function EnsureDirectoryExists([string] $path)
{
    New-Item -ItemType Directory -Force -Path $path *>$null
}

function Execute-WithRetry([ScriptBlock] $command) {
    $attemptCount = 0
    $operationIncomplete = $true
    $sleepBetweenFailures = 5
    $maxFailures = 5

    while ($operationIncomplete -and $attemptCount -lt $maxFailures) {
        $attemptCount = ($attemptCount + 1)

        if ($attemptCount -ge 2) {
            Write-Host "Waiting for $sleepBetweenFailures seconds before retrying..."
            Start-Sleep -s $sleepBetweenFailures
            Write-Host "Retrying..."
        }

        try {
            & $command

            $operationIncomplete = $false
        } catch [System.Exception] {
            if ($attemptCount -lt ($maxFailures)) {
                Write-Host ("Attempt $attemptCount of $maxFailures failed: " + $_.Exception.Message)
            } else {
                throw
            }
        }
    }
}

function Get-AzureRmModuleInstalled {
    return $null -ne (Get-Command "Login-AzureRmAccount" -ErrorAction SilentlyContinue)
}

function Get-AzModuleInstalled {
    return $null -ne (Get-Command "Connect-AzAccount" -ErrorAction SilentlyContinue)
}

function Get-RunningInPowershellCore {
    return $PSVersionTable.PSVersion.Major -gt 5
}

function Initialize-AzContext {
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

    If (![string]::IsNullOrEmpty($OctopusOpenIdJwt)) {
        Write-Verbose "Az Modules: Authenticating with OpenID Connect Federated Token"
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

Execute-WithRetry{
    pushd $env:OctopusCalamariWorkingDirectory
    try {
        If ([System.Convert]::ToBoolean($OctopusAzureServicePrincipalOrOidc )) {

            # Depending on which version of Powershell we are running under will change which module context we want to initialize.            
            #   Powershell Core: Check Az then AzureRM (provide a warning and do nothing if AzureRM is installed)
            #   Windows Powershell: Check AzureRM then Az
            #
            # If a module is installed (e.g. AzureRM) then testing for it causes the module to be loaded along with its various assemblies
            # and dependencies. If we then test for the other module (e.g. Az) then it also loads it's module and assemblies which then
            # creates havoc when you try and call any context methods such as Disable-AzContextAutosave due to version differences etc. 
            # For this reason we'll only test the module we prefer and then if it exists initialize it and not the other one.
            if (Get-RunningInPowershellCore) {
                if (Get-AzModuleInstalled) {
                    Initialize-AzContext
                }
                elseif (Get-AzureRmModuleInstalled) {
                    # AzureRM has been deprecated since 2024-02-29 https://learn.microsoft.com/en-us/powershell/azure/azurerm-retirement-overview
                    Write-Warning "AzureRM module is deprecated since 2024-02-29, Az module is now required to authenticate with Azure. See https://learn.microsoft.com/en-us/powershell/azure/azurerm-retirement-overview for more details."
                }                
            }            
            else { 
                # Windows Powershell
                if (Get-AzModuleInstalled) {
                    Initialize-AzContext
                }
                elseif (Get-AzureRmModuleInstalled) {
                    # AzureRM has been deprecated since 2024-02-29 https://learn.microsoft.com/en-us/powershell/azure/azurerm-retirement-overview
                    Write-Warning "AzureRM module is deprecated since 2024-02-29, Az module is now required to authenticate with Azure. See https://learn.microsoft.com/en-us/powershell/azure/azurerm-retirement-overview for more details."
                }
            }
            
            If (!$OctopusDisableAzureCLI -or $OctopusDisableAzureCLI -like [Boolean]::FalseString) {
                try {
                    # authenticate with the Azure CLI
                    Write-Host "##octopus[stdout-verbose]"

                    # Config directory is set to make sure that our security is right for the step running it  
                    # and not using the one in the default config dir to avoid issues with user defined ones 
                    $env:AZURE_CONFIG_DIR = [System.IO.Path]::Combine($env:OctopusCalamariWorkingDirectory, "azure-cli") 
                    EnsureDirectoryExists($env:AZURE_CONFIG_DIR) 
 
                    # The azure extensions directory is getting overridden above when we set the azure config dir (undocumented behavior). 
                    # Set the azure extensions directory to the value of $OctopusAzureExtensionsDirectory if specified, 
                    # otherwise, back to the default value of $HOME\.azure\cliextension.
                    if($OctopusAzureExtensionsDirectory) 
                    { 
                        Write-Host "Setting Azure CLI extensions directory to $OctopusAzureExtensionsDirectory" 
                        $env:AZURE_EXTENSION_DIR = $OctopusAzureExtensionsDirectory 
                    } else { 
                        $env:AZURE_EXTENSION_DIR = "$($HOME)\.azure\cliextensions" 
                    } 

                    $previousErrorAction = $ErrorActionPreference
                    $ErrorActionPreference = "Continue"

                    az cloud set --name $OctopusAzureEnvironment 2>$null 3>$null
                    $ErrorActionPreference = $previousErrorAction

                    Write-Host "Azure CLI: Authenticating with Service Principal"

                    $loginArgs = @();

                    If (![string]::IsNullOrEmpty($OctopusOpenIdJwt)) {
                        # Use the full argument because of https://github.com/Azure/azure-cli/issues/12105
                        $loginArgs += @("--username=$(ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusAzureADClientId)))");
                        $loginArgs += @("--tenant=$(ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusAzureADTenantId)))");
                        $loginArgs += @("--federated-token=$(ConvertTo-QuotedString($OctopusOpenIdJwt))");

                        Write-Host "az login --service-principal $loginArgs"
                        az login --service-principal $loginArgs
                    }
                    else {
                        # Use the full argument because of https://github.com/Azure/azure-cli/issues/12105
                        $loginArgs += @("--username=$(ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusAzureADClientId)))");
                        $loginArgs += @("--password=$(ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusAzureADPassword)))");
                        $loginArgs += @("--tenant=$(ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusAzureADTenantId)))");

                        Write-Host "az login --service-principal $loginArgs"
                        az login --service-principal $loginArgs
                    }

                    Write-Host "Azure CLI: Setting active subscription to $OctopusAzureSubscriptionId"
                    az account set --subscription $OctopusAzureSubscriptionId

                    Write-Host "##octopus[stdout-default]"
                    Write-Verbose "Successfully authenticated with the Azure CLI"
                } catch  {
                    # failed to authenticate with Azure CLI
                    Write-Verbose "Failed to authenticate with Azure CLI"
                    Write-Verbose $_.Exception.Message
                }
            }
        } Else {
            # Authenticate via Management Certificate
            Write-Verbose "Loading the management certificate"
            Add-Type -AssemblyName "System"
            $certificate = new-object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @($OctopusAzureCertificateFileName, $OctopusAzureCertificatePassword, ([System.Security.Cryptography.X509Certificates.X509KeyStorageFlags] "PersistKeySet", "Exportable"))
            $AzureEnvironment = Get-AzureEnvironment | Where-Object {$_.Name -eq $OctopusAzureEnvironment}

            if (!$AzureEnvironment)
            {
                Write-Error "No Azure environment could be matched given name $OctopusAzureEnvironment"
                exit 2
            }

            $azureProfile = New-AzureProfile -SubscriptionId $OctopusAzureSubscriptionId -StorageAccount $OctopusAzureStorageAccountName -Certificate $certificate -Environment $AzureEnvironment
            $azureProfile.Save(".\AzureProfile.json")
            Select-AzureProfile -Profile $azureProfile | Out-Null
        }
    }
    finally {
        popd
    }
}

Write-Verbose "Invoking target script $OctopusAzureTargetScript with $OctopusAzureTargetScriptParameters parameters"

try {
    Invoke-Expression ". `"$OctopusAzureTargetScript`" $OctopusAzureTargetScriptParameters"
} catch {
    # Warn if FIPS 140 compliance required when using Service Management SDK
    if ([System.Security.Cryptography.CryptoConfig]::AllowOnlyFipsAlgorithms -and ![System.Convert]::ToBoolean($OctopusAzureServicePrincipalOrOidc)) {
        Write-Warning "The Azure Service Management SDK is not FIPS 140 compliant. http://g.octopushq.com/FIPS"
    }

    throw
} finally {
    If (!$OctopusDisableAzureCLI -or $OctopusDisableAzureCLI -like [Boolean]::FalseString) {
        try {
            # Save the last exit code so az logout doesn't clobber it
            $previousLastExitCode = $LastExitCode
            $previousErrorAction = $ErrorActionPreference
            $ErrorActionPreference = "Continue"
            az logout 2>$null 3>$null
        } finally {
            # restore the previous last exit code
            $LastExitCode = $previousLastExitCode
            $ErrorActionPreference = $previousErrorAction
        }
    }
}
