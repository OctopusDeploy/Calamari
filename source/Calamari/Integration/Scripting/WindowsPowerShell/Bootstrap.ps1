param([string]$key="")

$ErrorActionPreference = 'Stop'

# All PowerShell scripts invoked by Calamari will be bootstrapped using this script. This script:
#  1. Declares/overrides various functions for scripts to use
#  2. Loads the $OctopusParameters variables
#  3. Sets a few defaults, like aborting scripts when an error is encountered
#  4. Invokes (using dot-sourcing) the target PowerShell script

# -----------------------------------------------------------------
# Functions
# -----------------------------------------------------------------

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
    if ($name -eq """") 
    {
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
	# Try AesCryptoServiceProvider first (requires .NET 3.5+), otherwise fall back to AesManaged (.NET 2.0)
	# Note using AesManaged will fail in FIPS compliant environments: https://support.microsoft.com/en-us/kb/811833
	Add-Type -AssemblyName System.Core -ErrorAction SilentlyContinue
	$aes = [System.Security.Cryptography.Aes] (New-Object System.Security.Cryptography.AesCryptoServiceProvider -ErrorAction SilentlyContinue)
	if ($aes -eq $null) {
		$aes = [System.Security.Cryptography.Aes] (New-Object System.Security.Cryptography.AesManaged)
	}

	$aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
	$aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
	$aes.KeySize = 128
	$aes.BlockSize = 128
	$aes.Key = [System.Convert]::FromBase64String($key)
	$aes.IV =[System.Convert]::FromBase64String($iv)
	$dec = [System.Security.Cryptography.ICryptoTransform]$aes.CreateDecryptor()

	$ms = new-Object IO.MemoryStream @(,[System.Convert]::FromBase64String($Encrypted)) 
	$cs = new-Object Security.Cryptography.CryptoStream $ms,$dec,"Read" 
	$sr = new-Object IO.StreamReader $cs 
	Write-Output $sr.ReadToEnd()
	$sr.Dispose() 
	$cs.Dispose() 
	$ms.Dispose() 
	$dec.Dispose()
	
	# The Aes base class implemented IDiposable explicitly in .NET 3.5, so 
	# the line below would fail under PowerShell 2.0 / CLR 2.0
	try {
		$aes.Dispose()
	} catch {}
}

function InitializeProxySettings() 
{
	$proxyUsername = $env:TentacleProxyUsername
	$proxyPassword = $env:TentacleProxyPassword

	if ([string]::IsNullOrEmpty($proxyUsername)) 
	{
		[System.Net.WebRequest]::DefaultWebProxy.Credentials = [System.Net.CredentialCache]::DefaultCredentials
	}
	else 
	{
		[System.Net.WebRequest]::DefaultWebProxy.Credentials = New-Object System.Net.NetworkCredential($proxyUsername, $proxyPassword)
	}
}

# -----------------------------------------------------------------
# Variables
# -----------------------------------------------------------------

{{VariableDeclarations}}


# -----------------------------------------------------------------
# Defaults
# -----------------------------------------------------------------

InitializeProxySettings

# -----------------------------------------------------------------
# Invoke target script
# -----------------------------------------------------------------
. '{{TargetScriptFile}}'

# -----------------------------------------------------------------
# Ensure we exit with whatever exit code the last exe used
# -----------------------------------------------------------------

if ((test-path variable:global:lastexitcode)) 
{ 
	exit $LastExitCode 
}
