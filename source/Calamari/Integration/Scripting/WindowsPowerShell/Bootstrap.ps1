param([string]$OctopusKey="")
{{StartOfBootstrapScriptDebugLocation}}
$ErrorActionPreference = 'Stop'

# All PowerShell scripts invoked by Calamari will be bootstrapped using this script. This script:
#  1. Declares/overrides various functions for scripts to use
#  2. Loads the $OctopusParameters variables
#  3. Sets a few defaults, like aborting scripts when an error is encountered
#  4. Invokes (using dot-sourcing) the target PowerShell script

# -----------------------------------------------------------------
# Functions
# -----------------------------------------------------------------

function Log-VersionTable
{
	Write-Verbose ($PSVersionTable | Out-String)
}

function Log-EnvironmentInformation
{
	if ($OctopusParameters.ContainsKey("Octopus.Action.Script.SuppressEnvironmentLogging")) {
		if ($OctopusParameters["Octopus.Action.Script.SuppressEnvironmentLogging"] -eq "True") {
			return;
		}
	}

	Write-Host "##octopus[stdout-verbose]"
	Write-Host "PowerShell Environment Information:"
	SafelyLog-EnvironmentVars
	SafelyLog-PathVars
	SafelyLog-ProcessVars
	SafelyLog-ComputerInfoVars
	Write-Host "##octopus[stdout-default]"
}

function SafelyLog-EnvironmentVars
{
	Try
	{
		$operatingSystem = [System.Environment]::OSVersion.ToString()
		Write-Host "  OperatingSystem: $($operatingSystem)"
		
		$osBitVersion = If ([System.Environment]::Is64BitOperatingSystem) {"x64"} Else {"x86"}
		Write-Host "  OsBitVersion: $($osBitVersion)"

		$is64BitProcess = [System.Environment]::Is64BitProcess.ToString()
		Write-Host "  Is64BitProcess: $($is64BitProcess)"

		$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
		Write-Host "  CurrentUser: $($currentUser)"

		$machineName = [System.Environment]::MachineName
		Write-Host "  MachineName: $($machineName)"

		$processorCount = [System.Environment]::ProcessorCount.ToString()
		Write-Host "  ProcessorCount: $($processorCount)"
	}
	Catch
	{
		# silently fail.
	}
}

function SafelyLog-PathVars
{
	Try
	{
		$currentDirectory = [System.IO.Directory]::GetCurrentDirectory()
		Write-Host "  CurrentDirectory: $($currentDirectory)"

		$currentLocation = Get-Location
		Write-Host "  CurrentLocation: $($currentLocation)"
		
		$tempPath = [System.IO.Path]::GetTempPath()
		Write-Host "  TempDirectory: $($tempPath)"
	}
	Catch
	{
		# silently fail.
	}
}

function SafelyLog-ProcessVars
{
	Try
	{
		$hostProcess = [System.Diagnostics.Process]::GetCurrentProcess().ProcessName
		Write-Host "  HostProcessName: $($hostProcess)"
	}
	Catch
	{
		# silently fail.
	}
}

function SafelyLog-ComputerInfoVars
{
	Try
	{
		$OperatingSystem = (Get-WmiObject Win32_OperatingSystem)

		$totalVisibleMemorySize = $OperatingSystem.TotalVisibleMemorySize
		Write-Host "  TotalPhysicalMemory: $($totalVisibleMemorySize) KB"

		$freePhysicalMemory = $OperatingSystem.FreePhysicalMemory
		Write-Host "  AvailablePhysicalMemory: $($freePhysicalMemory) KB"
	}
	Catch
	{
		# silently fail.
	}
}

function Import-ScriptModule([string]$moduleName, [string]$moduleFilePath)
{
	Try 
	{
		Write-Verbose "Importing Script Module '$moduleName' from '$moduleFilePath'"
		Import-Module $moduleFilePath
	}
	Catch
	{
		Write-Warning "Failed to import Script Module '$moduleName'"
		Throw
	}
	Finally
	{
		# Once we've loaded (or failed to load) the script module, 
		# delete the script module file from the filesystem
		# https://github.com/OctopusDeploy/Issues/issues/3895
		Remove-Item $moduleFilePath -Force -ErrorAction SilentlyContinue
	}
}

function Convert-ServiceMessageValue([string]$value)
{
	$valueBytes = [System.Text.Encoding]::UTF8.GetBytes($value)
	return [Convert]::ToBase64String($valueBytes)
}

function Set-OctopusVariable([string]$name, [string]$value) 
{
	$name = Convert-ServiceMessageValue($name)
	$value = Convert-ServiceMessageValue($value)

	Write-Host "##octopus[setVariable name='$($name)' value='$($value)']"
}

function Convert-ToServiceMessageParameter([string]$name, [string]$value)
{
    $value = Convert-ServiceMessageValue($value)
    $param = "$($name)='$($value)'"
	return $param
}

function New-OctopusAzureServicePrincipalAccount([string]$name, [string]$azureSubscriptionId, [string]$azureApplicationId, [string]$azureTenantId, [string]$azurePassword, [string]$azureEnvironment, [string]$azureBaseUri, [string]$azureResourceManagementBaseUri, [switch]$updateIfExisting) 
{
	$name = Convert-ToServiceMessageParameter -name "name" -value $name 
 	$azureSubscription = Convert-ToServiceMessageParameter -name "azSubscriptionId" -value $azureSubscriptionId
 	$azureApplicationId = Convert-ToServiceMessageParameter -name "azApplicationId" -value $azureApplicationId
 	$azureTenantId = Convert-ToServiceMessageParameter -name "azTenantId" -value $azureTenantId
 	$azurePassword = Convert-ToServiceMessageParameter -name "azPassword" -value $azurePassword
	$type = Convert-ToServiceMessageParameter -name "type" -value "serviceprincipal"
	$updateIfExistingParameter = Convert-ToServiceMessageParameter -name "updateExisting" -value $updateIfExisting

	$parameters = $type, $name, $azureSubscription, $azureApplicationId, $azureTenantId, $azurePassword, $updateIfExistingParameter -join ' '

	if (![string]::IsNullOrEmpty($azureEnvironment))
	{
		$azureEnvironment = Convert-ToServiceMessageParameter -name "azEnvironment" -value $azureEnvironment
		$azureBaseUri = Convert-ToServiceMessageParameter -name "azBaseUri" -value $azureBaseUri
		$azureResourceManagementBaseUri = Convert-ToServiceMessageParameter -name "azResourceManagementBaseUri" -value $azureResourceManagementBaseUri
		$parameters = $parameters, $azureEnvironment, $azureBaseUri, $azureResourceManagementBaseUri -join ' '
	}
 	
    Write-Host "##octopus[create-azureaccount $($parameters)]"
}

function New-OctopusAzureWebAppTarget([string]$name, [string]$azureWebApp, [string]$azureResourceGroupName, [string]$octopusAccountIdOrName, [string]$octopusRoles, [switch]$updateIfExisting) 
{
	$name = Convert-ToServiceMessageParameter -name "name" -value $name 
 	$azureWebApp = Convert-ToServiceMessageParameter -name "webAppName" -value $azureWebApp
    $azureResourceGroupName = Convert-ToServiceMessageParameter -name "resourceGroupName" -value $azureResourceGroupName
    $octopusAccountIdOrName = Convert-ToServiceMessageParameter -name "account" -value $octopusAccountIdOrName
	$octopusRoles = Convert-ToServiceMessageParameter -name "roles" -value $octopusRoles
	$updateIfExistingParameter = Convert-ToServiceMessageParameter -name "updateExisting" -value $updateIfExisting

	$parameters = $name, $azureWebApp, $azureResourceGroupName, $octopusAccountIdOrName, $octopusRoles, $updateIfExistingParameter -join ' '

    Write-Host "##octopus[create-azurewebapptarget $($parameters)]"
}

function New-OctopusAzureServiceFabricTarget([string]$name, [string]$azureConnectionEndpoint, [string]$azureSecurityMode, [string]$azureCertificateThumbprint, [string]$azureActiveDirectoryUsername, [string]$azureActiveDirectoryPassword, [string]$certificateStoreLocation, [string]$certificateStoreName, [string]$octopusCertificateIdOrName, [string]$octopusRoles, [switch]$updateIfExisting) 
{
	$name = Convert-ToServiceMessageParameter -name "name" -value $name 
	$azureConnectionEndpoint = Convert-ToServiceMessageParameter -name "connectionEndpoint" -value $azureConnectionEndpoint
	$azureSecurityMode = Convert-ToServiceMessageParameter -name "securityMode" -value $azureSecurityMode
	$azureCertificateThumbprint = Convert-ToServiceMessageParameter -name "certificateThumbprint" -value $azureCertificateThumbprint
	$azureActiveDirectoryUsername = Convert-ToServiceMessageParameter -name "activeDirectoryUsername" -value $azureActiveDirectoryUsername
	$azureActiveDirectoryPassword = Convert-ToServiceMessageParameter -name "activeDirectoryPassword" -value $azureActiveDirectoryPassword
	$certificateStoreLocation = Convert-ToServiceMessageParameter -name "certificateStoreLocation" -value $certificateStoreLocation
	$certificateStoreName = Convert-ToServiceMessageParameter -name "certificateStoreName" -value $certificateStoreName
	$octopusCertificateIdOrName = Convert-ToServiceMessageParameter -name "certificate" -value $octopusCertificateIdOrName
	$octopusRoles = Convert-ToServiceMessageParameter -name "roles" -value $octopusRoles
	$updateIfExistingParameter = Convert-ToServiceMessageParameter -name "updateExisting" -value $updateIfExisting

	$parameters = $name, $azureConnectionEndpoint, $azureSecurityMode, $azureCertificateThumbprint, $azureActiveDirectoryUsername, $azureActiveDirectoryPassword, $certificateStoreLocation, $certificateStoreName, $octopusCertificateIdOrName, $octopusRoles, $updateIfExistingParameter -join ' '

	Write-Host "##octopus[create-azureservicefabrictarget $($parameters)]"
}

function New-OctopusAzureCloudServiceTarget([string]$name, [string]$azureCloudServiceName, [string]$azureStorageAccount, [string]$azureDeploymentSlot, [string]$swap, [string]$instanceCount, [string]$octopusAccountIdOrName, [string]$octopusRoles, [switch]$updateIfExisting) 
{
	$name = Convert-ToServiceMessageParameter -name "name" -value $name 
	$azureCloudServiceName = Convert-ToServiceMessageParameter -name "azureCloudServiceName" -value $azureCloudServiceName
	$azureStorageAccount = Convert-ToServiceMessageParameter -name "azureStorageAccount" -value $azureStorageAccount
	$azureDeploymentSlot = Convert-ToServiceMessageParameter -name "azureDeploymentSlot" -value $azureDeploymentSlot
	$swap = Convert-ToServiceMessageParameter -name "swap" -value $swap
	$instanceCount = Convert-ToServiceMessageParameter -name "instanceCount" -value $instanceCount
	$octopusAccountIdOrName = Convert-ToServiceMessageParameter -name "account" -value $octopusAccountIdOrName
	$octopusRoles = Convert-ToServiceMessageParameter -name "roles" -value $octopusRoles
	$updateIfExistingParameter = Convert-ToServiceMessageParameter -name "updateExisting" -value $updateIfExisting

	$parameters = $name, $azureCloudServiceName, $azureStorageAccount, $azureDeploymentSlot, $swap, $instanceCount, $octopusAccountIdOrName, $octopusRoles, $updateIfExistingParameter -join ' '

	Write-Host "##octopus[create-azurecloudservicetarget $($parameters)]"
}

function Remove-OctopusTarget([string] $targetIdOrName)
{
	$targetIdOrName = Convert-ToServiceMessageParameter -name "machine" -value $targetIdOrName
	$parameters = $targetIdOrName -join ' '
	Write-Host "##octopus[delete-target $($parameters)]"
}

function Fail-Step([string] $message)
{
	if($message)
	{
		$message = Convert-ServiceMessageValue($message)
		Write-Host "##octopus[resultMessage message='$($message)']"
	}
	exit -1
}

function New-OctopusArtifact([string]$path, [string]$name="""") 
{
	if ((Test-Path $path) -eq $false) {
		Write-Verbose "There is no file at '$path' right now. Writing the service message just in case the file is available when the artifacts are collected at a later point in time."
	}

	if ($name -eq """")	{
		$name = [System.IO.Path]::GetFileName($path)
	}
	$servicename = Convert-ServiceMessageValue($name)

	$length = ([System.IO.FileInfo]$path).Length;
	if (!$length) {
		$length = 0;
	}
	$length = Convert-ServiceMessageValue($length.ToString());

	$path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($path)
	$path = [System.IO.Path]::GetFullPath($path)
    $servicepath = Convert-ServiceMessageValue($path)

    Write-Verbose "Artifact $name will be collected from $path after this step completes"
	Write-Host "##octopus[createArtifact path='$($servicepath)' name='$($servicename)' length='$($length)']"
}

function Write-Debug([string]$message)
{
	Write-Verbose $message
}

function Write-Verbose([string]$message)
{
	Write-Host "##octopus[stdout-verbose]"
	Write-Host $message
	Write-Host "##octopus[stdout-default]"
}

function Write-Highlight([string]$message)
{
	Write-Host "##octopus[stdout-highlight]"
	Write-Host $message
	Write-Host "##octopus[stdout-default]"
}

function Write-Wait([string]$message)
{
	Write-Host "##octopus[stdout-wait]"
	Write-Host $message
	Write-Host "##octopus[stdout-default]"
}

function Write-Warning()
{
	[CmdletBinding()]
	param([string]$message)

	if ($WarningPreference -eq 'SilentlyContinue') {
		return
	}
	Write-Host "##octopus[stdout-warning]"
	Write-Host $message
	Write-Host "##octopus[stdout-default]"
}

function Decrypt-Variables($iv, $Encrypted) 
{
    function ConvertFromBase64String($str)
    {
        # "nul" is a special string used by Calamari to represent null. "null" is not used as it is a valid Base64 string. 
        if($str -eq "nul")
        {
            return $null;
        }
        else
        {
            [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($str))
        }
    }

    $parameters = New-Object 'System.Collections.Generic.Dictionary[String,String]' (,[System.StringComparer]::OrdinalIgnoreCase)

	# Try AesCryptoServiceProvider first (requires .NET 3.5+), otherwise fall back to RijndaelManaged (.NET 2.0)
	# Note using RijndaelManaged will fail in FIPS compliant environments: https://support.microsoft.com/en-us/kb/811833
	$algorithm = $null
	try {
		Add-Type -AssemblyName System.Core
		$algorithm = [System.Security.Cryptography.SymmetricAlgorithm] (New-Object System.Security.Cryptography.AesCryptoServiceProvider)
	} catch {
		Write-Verbose "Could not load AesCryptoServiceProvider, falling back to RijndaelManaged (.NET 2.0)."
		$algorithm = [System.Security.Cryptography.SymmetricAlgorithm] (New-Object System.Security.Cryptography.RijndaelManaged)
	}

	$algorithm.Mode = [System.Security.Cryptography.CipherMode]::CBC
	$algorithm.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
	$algorithm.KeySize = 128
	$algorithm.BlockSize = 128 # AES is just Rijndael with a fixed block size
	$algorithm.Key = [System.Convert]::FromBase64String($OctopusKey)
	$algorithm.IV =[System.Convert]::FromBase64String($iv)
	$decryptor = [System.Security.Cryptography.ICryptoTransform]$algorithm.CreateDecryptor()

	$memoryStream = new-Object IO.MemoryStream @(,[System.Convert]::FromBase64String($Encrypted)) 
	$cryptoStream = new-Object Security.Cryptography.CryptoStream $memoryStream,$decryptor,"Read" 
	$streamReader = new-Object IO.StreamReader $cryptoStream 
	while($streamReader.EndOfStream -eq $false)
    {
        $parts = $streamReader.ReadLine().Split("$")
        # The seemingly superfluous '-as' below was for PowerShell 2.0.  Without it, a cast exception was thrown when trying to add the object to a generic collection. 
        $parameters[(ConvertFromBase64String $parts[0])] = ConvertFromBase64String $parts[1] -as [string]
    }
	$streamReader.Dispose() | Out-Null
	$cryptoStream.Dispose() | Out-Null
	$memoryStream.Dispose() | Out-Null

	# RijndaelManaged/RijndaelManagedTransform implemented IDiposable explicitly
	[System.IDisposable].GetMethod("Dispose").Invoke($decryptor, @()) | Out-Null
	[System.IDisposable].GetMethod("Dispose").Invoke($algorithm, @()) | Out-Null
	
	return $parameters
}

function Initialize-ProxySettings() 
{
	$proxyUsername = $env:TentacleProxyUsername
	$proxyPassword = $env:TentacleProxyPassword
	$proxyHost = $env:TentacleProxyHost
	[int]$proxyPort = $env:TentacleProxyPort
	
	$useSystemProxy = [string]::IsNullOrEmpty($proxyHost) 
	
	if($useSystemProxy)
	{
		$proxy = [System.Net.WebRequest]::GetSystemWebProxy()
	}	
	else
	{
		$proxyUri = [System.Uri]"http://${proxyHost}:$proxyPort"
		$proxy = New-Object System.Net.WebProxy($proxyUri)
	}

	if ([string]::IsNullOrEmpty($proxyUsername)) 
	{
		if($useSystemProxy)
		{
			$proxy.Credentials = [System.Net.CredentialCache]::DefaultCredentials
		}
		else
		{
			$proxy.Credentials = New-Object System.Net.NetworkCredential("","")
		}
	}
	else 
	{
		$proxy.Credentials = New-Object System.Net.NetworkCredential($proxyUsername, $proxyPassword)
	}

	[System.Net.WebRequest]::DefaultWebProxy = $proxy
}

function Execute-WithRetry([ScriptBlock] $command, [int] $maxFailures = 3, [int] $sleepBetweenFailures = 1) {
	$attemptCount = 0
	$operationIncomplete = $true

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

function Import-CalamariModules() {
	if ($OctopusParameters.ContainsKey("Octopus.Calamari.Bootstrapper.ModulePaths")) {
		$calamariModulePaths = $OctopusParameters["Octopus.Calamari.Bootstrapper.ModulePaths"].Split(";", [StringSplitOptions]'RemoveEmptyEntries')
		foreach ($calamariModulePath in $calamariModulePaths) {
		    if($calamariModulePath.EndsWith(".psd1")) {
		        Import-Module –Name $calamariModulePath.Replace("{{TentacleHome}}", $env:TentacleHome)
		    } else {
        		$env:PSModulePath = $calamariModulePath + ";" + $env:PSModulePath
		    }
		}
	}
}

Log-VersionTable

# -----------------------------------------------------------------
# Variables
# -----------------------------------------------------------------
{{BeforeVariablesDebugLocation}}
$MaximumVariableCount=32768
$OctopusParameters = Decrypt-Variables '{{VariablesIV}}' @'
{{EncryptedVariablesString}}
'@

{{LocalVariableDeclarations}}

# -----------------------------------------------------------------
# Script Modules - after variables
# -----------------------------------------------------------------
{{BeforeScriptModulesDebugLocation}}
{{ScriptModules}}

# -----------------------------------------------------------------
# Defaults
# -----------------------------------------------------------------

Initialize-ProxySettings

Log-EnvironmentInformation

# -----------------------------------------------------------------
# Invoke target script
# -----------------------------------------------------------------
Import-CalamariModules

# -----------------------------------------------------------------
# Invoke target script
# -----------------------------------------------------------------
{{BeforeLaunchingUserScriptDebugLocation}}
. '{{TargetScriptFile}}' {{ScriptParameters}}

# -----------------------------------------------------------------
# Ensure we exit with whatever exit code the last exe used
# -----------------------------------------------------------------

if ((test-path variable:global:lastexitcode)) 
{
	exit $LastExitCode 
}
