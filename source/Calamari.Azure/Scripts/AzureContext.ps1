## Octopus Azure Context script, version 1.0
## --------------------------------------------------------------------------------------
##
## This script is used to load the Azure Powershell module and select the Azure subscription
##
## The script is passed the following parameters.
##
##   $OctopusAzureTargetScript = "..."
##   $OctopusAzureTargetScriptParameters = "..."
##   $OctopusUseServicePrincipal = "false"
##   $OctopusAzureSubscriptionId = "..."
##   $OctopusAzureStorageAccountName = "..."
##   $OctopusAzureCertificateFileName = "..."
##   $OctopusAzureCertificatePassword = "..."
##   $OctopusAzureADTenantId = "..."
##   $OctopusAzureADClientId = "..."
##   $OctopusAzureADPassword = "..."
##   $OctopusAzureEnvironment = "..."
##   $OctopusDisableAzureCLI = "..."

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

Execute-WithRetry{
    pushd $env:OctopusCalamariWorkingDirectory
    try {
        If ([System.Convert]::ToBoolean($OctopusUseServicePrincipal)) {
            # Authenticate via Service Principal
            $securePassword = ConvertTo-SecureString $OctopusAzureADPassword -AsPlainText -Force
            $creds = New-Object System.Management.Automation.PSCredential ($OctopusAzureADClientId, $securePassword)

            # Turn off context autosave, as this will make all authentication occur in memory, and isolate each session from the context changes in other sessions
            Disable-AzureRMContextAutosave -Scope Process

            $AzureEnvironment = Get-AzureRmEnvironment -Name $OctopusAzureEnvironment
            if (!$AzureEnvironment) {
                Write-Error "No Azure environment could be matched given the name $OctopusAzureEnvironment"
                exit -2
            }

            Write-Verbose "Authenticating with Service Principal"

            # Force any output generated to be verbose in Octopus logs.
            Write-Host "##octopus[stdout-verbose]"
            Login-AzureRmAccount -Credential $creds -TenantId $OctopusAzureADTenantId -SubscriptionId $OctopusAzureSubscriptionId -Environment $AzureEnvironment -ServicePrincipal
            Write-Host "##octopus[stdout-default]"

            If (!$OctopusDisableAzureCLI -or $OctopusDisableAzureCLI -like [Boolean]::FalseString) {
                try {
                    $badCharsRegex = '.*[&|].*'
                    $passwordContainsBadChars = $OctopusAzureADPassword -match $badCharsRegex
                    $userNameContainsBadChars = $OctopusAzureADClientId -match $badCharsRegex
                    if ($passwordContainsBadChars -or $userNameContainsBadChars) {
                        Write-Warning 'The Azure Account credentials contains either a | or a & character. This can cause issues with the azure CLI invocation and should be avoided.'
                        throw 'Invalid characters were passed into azure login credentials'
                    }
                
                    # authenticate with the Azure CLI
                    Write-Host "##octopus[stdout-verbose]"
                
                    $env:AZURE_CONFIG_DIR = [System.IO.Path]::Combine($env:OctopusCalamariWorkingDirectory, "azure-cli")
                    EnsureDirectoryExists($env:AZURE_CONFIG_DIR)

                    $previousErrorAction = $ErrorActionPreference
                    $ErrorActionPreference = "Continue"
                    az cloud set --name $OctopusAzureEnvironment 2>$null 3>$null 
                    $ErrorActionPreference = $previousErrorAction
                    
                    Write-Host "Azure CLI: Authenticating with Service Principal"
                    az login --service-principal -u "$OctopusAzureADClientId" -p "$OctopusAzureADPassword" --tenant $OctopusAzureADTenantId 
        
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
                exit -2
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
    Invoke-Expression ". $OctopusAzureTargetScript $OctopusAzureTargetScriptParameters"
} catch {
    # Warn if FIPS 140 compliance required when using Service Management SDK
    if ([System.Security.Cryptography.CryptoConfig]::AllowOnlyFipsAlgorithms -and ![System.Convert]::ToBoolean($OctopusUseServicePrincipal)) {
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

