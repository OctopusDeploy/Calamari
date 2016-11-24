param([string]$OctopusKey="")

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
		if ($OctopusParameters["Octopus.Action.Script.SuppressEnvironmentLogging"] == "True") {
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

function New-OctopusArtifact([string]$path, [string]$name="""") 
{
	if ((Test-Path $path) -eq $false) {
		Write-Verbose "There is no file at '$path' right now. Writing the service message just in case the file is available when the artifacts are collected at a later point in time."
	}

	if ($name -eq """")	{
		$name = [System.IO.Path]::GetFileName($path)
	}
	$name = Convert-ServiceMessageValue($name)

	$length = ([System.IO.FileInfo]$path).Length;
	if (!$length) {
		$length = 0;
	}
	$length = Convert-ServiceMessageValue($length.ToString());

	$path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($path)
	$path = [System.IO.Path]::GetFullPath($path)
	$path = Convert-ServiceMessageValue($path)

	Write-Host "##octopus[createArtifact path='$($path)' name='$($name)' length='$($length)']"
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

function Write-Warning([string]$message)
{
	Write-Host "##octopus[stdout-warning]"
	Write-Host $message
	Write-Host "##octopus[stdout-default]"
}

function Decrypt-String($Encrypted, $iv) 
{
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
	Write-Output $streamReader.ReadToEnd()
	$streamReader.Dispose() | Out-Null
	$cryptoStream.Dispose() | Out-Null
	$memoryStream.Dispose() | Out-Null

	# RijndaelManaged/RijndaelManagedTransform implemented IDiposable explicitly
	[System.IDisposable].GetMethod("Dispose").Invoke($decryptor, @()) | Out-Null
	[System.IDisposable].GetMethod("Dispose").Invoke($algorithm, @()) | Out-Null
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

Log-VersionTable

# -----------------------------------------------------------------
# Variables
# -----------------------------------------------------------------

{{VariableDeclarations}}

# -----------------------------------------------------------------
# Script Modules - after variables
# -----------------------------------------------------------------

{{ScriptModules}}

# -----------------------------------------------------------------
# Defaults
# -----------------------------------------------------------------

Initialize-ProxySettings

Log-EnvironmentInformation

# -----------------------------------------------------------------
# Invoke target script
# -----------------------------------------------------------------
. '{{TargetScriptFile}}' {{ScriptParameters}}

# -----------------------------------------------------------------
# Ensure we exit with whatever exit code the last exe used
# -----------------------------------------------------------------

if ((test-path variable:global:lastexitcode)) 
{
	exit $LastExitCode 
}
