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


function Decrypt-String($Encrypted) 
{ 
	$passwordBytes = [Text.Encoding]::UTF8.GetBytes($passwd) 
	$encryptedBytes = [System.Convert]::FromBase64String($Encrypted) 

	#Get salt from encrypted text
	$salt = New-Object Byte[] 8
	[System.Buffer]::BlockCopy($encryptedBytes, 8, $salt, 0, 8)

	#Remove salt from encrypted text
	$aesDataLength = $encryptedBytes.Length - 16
	$aesData = New-Object Byte[] $aesDataLength
	[System.Buffer]::BlockCopy($encryptedBytes, 16, $aesData, 0, $aesDataLength)


	$md5=[System.Security.Cryptography.MD5]::Create()
		#Get Key
		$preKeyLength = $passwordBytes.Length + $salt.Length
		$preKey = New-Object Byte[] $preKeyLength
		[System.Buffer]::BlockCopy($passwordBytes, 0, $preKey, 0, $passwordBytes.Length)
		[System.Buffer]::BlockCopy($salt, 0, $preKey, $passwordBytes.Length, $salt.Length)
		$key = $md5.ComputeHash($preKey)

		#Get IV
		$preIVLength = $key.Length + $preKeyLength
		$preIV =  New-Object Byte[] $preIVLength
		[System.Buffer]::BlockCopy($key, 0, $preIV, 0, $key.Length)
		[System.Buffer]::BlockCopy($preKey, 0, $preIV, $key.Length, $preKey.Length)
		$iv = $md5.ComputeHash($preIV)
	$md5.Dispose()

	$algoritm = new-Object System.Security.Cryptography.AesManaged;
	$algoritm.Mode = [System.Security.Cryptography.CipherMode]::CBC
	$algoritm.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
	$algoritm.KeySize = 128
	$algoritm.BlockSize = 128
	$algoritm.Key = $key
	$algoritm.IV =$iv

	$dec = $algoritm.CreateDecryptor()
	$ms = new-Object IO.MemoryStream @(,$aesData) 
	$cs = new-Object Security.Cryptography.CryptoStream $ms,$dec,"Read" 
	$sr = new-Object IO.StreamReader $cs 
	$text = $sr.ReadToEnd() 
	$sr.Close() 
	$cs.Close() 
	$ms.Close() 
	$r.Clear() 
	return $text

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
