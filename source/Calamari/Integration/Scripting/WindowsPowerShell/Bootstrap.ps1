param([string]$passwd="")

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


function Decrypt-String($Encrypted, $Passphrase=$passwd, $salt="SaltCrypto", $init="IV_Password") 
{ 
    if($Encrypted -is [string]){ 
        $Encrypted = [Convert]::FromBase64String($Encrypted) 
       } 
 
    $r = new-Object System.Security.Cryptography.RijndaelManaged 
    $pass = [Text.Encoding]::UTF8.GetBytes($Passphrase) 
    $salt = [Text.Encoding]::UTF8.GetBytes($salt) 
 
    $r.Key = (new-Object Security.Cryptography.PasswordDeriveBytes $pass, $salt, "SHA1", 5).GetBytes(32) #256/8 
    $r.IV = (new-Object Security.Cryptography.SHA1Managed).ComputeHash( [Text.Encoding]::UTF8.GetBytes($init) )[0..15] 
 
    $dec = $r.CreateDecryptor() 
    $ms = new-Object IO.MemoryStream @(,$Encrypted) 
    $cs = new-Object Security.Cryptography.CryptoStream $ms,$dec,"Read" 
    $sr = new-Object IO.StreamReader $cs 
    $result = $sr.ReadToEnd() 
    $sr.Close() 
    $cs.Close() 
    $ms.Close() 
    $r.Clear() 
	return $result;
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

$ErrorActionPreference = 'Stop'

# -----------------------------------------------------------------
# Invoke target script
# -----------------------------------------------------------------

. "{{TargetScriptFile}}"

# -----------------------------------------------------------------
# Ensure we exit with whatever exit code the last exe used
# -----------------------------------------------------------------

if ((test-path variable:global:lastexitcode)) 
{ 
	exit $LastExitCode 
}
