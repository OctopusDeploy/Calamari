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

if ($PSVersionTable.PSVersion.Major -lt 5)
{
    throw "These Azure commands are only supported in PowerShell versions 5 and above. This server is currently running PowerShell version $($PSVersionTable.PSVersion.ToString())."
}

$ErrorActionPreference = "Stop"
$EnabledAzureRmBundledModules = $false;
[bool]::TryParse($OctopusUseBundledAzureModules, [ref]$EnabledAzureRmBundledModules) | out-null
$AzModule = Get-Module -ListAvailable "Az.Accounts"
$AzureRmModule = Get-Module -ListAvailable "AzureRM.Profile"

function Check-CommandExists($Name)
{
    return [bool](Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function Get-OctoCommandAlias([string]$Value, [Regex]$IgnorePrefix ="^(AzureRm|Az)") {
    $match = $Value -match "^[a-zA-Z]+-"
    $verb = $matches[0];
    $name = $_.Name.Substring($verb.Length)

    $ignore = $IgnorePrefix.Match($name)
    if($null -ne $ignore){
        $name = $name.Substring($ignore.Length)
    }

    $alias = $verb + "OctoAz" + $name
    return $alias
}


function Get-OctoAliases(){
    $module = $null;

    if ($null -ne $AzModule)
    {
        if ($EnabledAzureRmBundledModules) {
            Write-Verbose "The AzureRM bundled modules can't be used when the Az Powershell modules are installed. Setting up context for Az ($($AzModule.Version)) instead."
        } else {
            Write-Verbose "Setting up context for Az ($($AzModule.Version))."
        }

        $module = "Az.Accounts";
    }
    elseif ($EnabledAzureRmBundledModules -and $null -ne $AzureRmModule)
    {
        Write-Verbose "Setting up context for bundled AzureRM ($($AzureRmModule.Version))."
        $module = "AzureRM.Profile"
    } elseif ($null -ne $AzureRmModule) {
        Write-Verbose "Setting up context for custom AzureRM ($($AzureRmModule.Version))."
        $module = "AzureRM.Profile"
    } else {
        Write-Warning "Azure modules could be found, unable to set up context."
    }

    $aliases = @{}

    if ($null -ne $module) {
        Get-Command -Module $module | % {
            $alias = Get-OctoCommandAlias -Value $_.Name

            $aliases[$alias] = $_
        }
    }
    return $aliases
}
function Execute-WithOctoAliases([ScriptBlock]$command, [HashTable]$aliases){
    $aliases.Keys | % { Set-Alias -Name $_ -Value $aliases[$_] -Option Private }
    . $command
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

if ($null -ne $AzModule -and $OctopusUseAzCompatibility -ceq 'True' -and (Check-CommandExists Enable-AzureRmAlias)) {
    if ($AzureRmModule){
        Write-Verbose "Unable to enable Az compatbility mode when AzureRmModules are installed"
    } else {
        Write-Verbose "Enabling AzureRm compatibility mode for Az modules"
        Enable-AzureRmAlias -Scope Process
    }
}

$resolvedAliases = Get-OctoAliases

Execute-WithRetry{
    Execute-WithOctoAliases -aliases $resolvedAliases {
        pushd $env:OctopusCalamariWorkingDirectory
        try {
            If ([System.Convert]::ToBoolean($OctopusUseServicePrincipal)) {
                # Authenticate via Service Principal
                $securePassword = ConvertTo-SecureString $OctopusAzureADPassword -AsPlainText -Force
                $creds = New-Object System.Management.Automation.PSCredential ($OctopusAzureADClientId, $securePassword)
                if (Check-CommandExists Disable-OctoAzContextAutosave) {
                    # Turn off context autosave, as this will make all authentication occur in memory, and isolate each session from the context changes in other sessions
                    Disable-OctoAzContextAutosave -Scope Process
                }
                
                $AzureEnvironment = Get-OctoAzEnvironment -Name $OctopusAzureEnvironment
                if (!$AzureEnvironment) {
                    Write-Error "No Azure environment could be matched given the name $OctopusAzureEnvironment"
                    exit -2
                }
    
                Write-Verbose "Authenticating with Service Principal"
    
                # Force any output generated to be verbose in Octopus logs.
                Write-Host "##octopus[stdout-verbose]"
                Login-OctoAzAccount -Credential $creds -TenantId $OctopusAzureADTenantId -SubscriptionId $OctopusAzureSubscriptionId -Environment $AzureEnvironment -ServicePrincipal
                Write-Host "##octopus[stdout-default]"
    
                If (!$OctopusDisableAzureCLI -or $OctopusDisableAzureCLI -like [Boolean]::FalseString) {
                    try {
                        # authenticate with the Azure CLI
                        Write-Host "##octopus[stdout-verbose]"
    
                        $env:AZURE_CONFIG_DIR = [System.IO.Path]::Combine($env:OctopusCalamariWorkingDirectory, "azure-cli")
                        EnsureDirectoryExists($env:AZURE_CONFIG_DIR)
    
                        $previousErrorAction = $ErrorActionPreference
                        $ErrorActionPreference = "Continue"
    
                        az cloud set --name $OctopusAzureEnvironment 2>$null 3>$null
                        $ErrorActionPreference = $previousErrorAction
    
                        Write-Host "Azure CLI: Authenticating with Service Principal"
    
                        $loginArgs = @();
                        $loginArgs += @("-u", (ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusAzureADClientId))));
                        $loginArgs += @("-p", (ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusAzureADPassword))));
                        $loginArgs += @("--tenant", (ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusAzureADTenantId))));
                        az login --service-principal $loginArgs
    
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
                $AzureEnvironment = Get-OctoAzEnvironment | Where-Object {$_.Name -eq $OctopusAzureEnvironment}
    
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

