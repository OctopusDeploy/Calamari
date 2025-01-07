param([string]$OctopusKey="")
{{StartOfBootstrapScriptDebugLocation}}
$ErrorActionPreference = 'Stop'
$PSNativeCommandArgumentPassing = 'Legacy'

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
	Write-Verbose ($PSVersionTable | Out-String -Width 200) # Calling Out-String without specify the width resulted in blank lines when running tests under MacOS 10.15 and Netcore 3.1 for some reason
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
	    $process = [System.Diagnostics.Process]::GetCurrentProcess();
		Write-Host "  HostProcess: $($process.ProcessName) ($($process.Id))"
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
		Import-Module -DisableNameChecking $moduleFilePath
	}
	Catch
	{
		[System.Console]::Error.WriteLine("Failed to import Script Module '$moduleName' from '$moduleFilePath'")
		[System.Console]::Error.WriteLine("$($error[0].CategoryInfo.Category): $($error[0].Exception.Message)")
		[System.Console]::Error.WriteLine($error[0].InvocationInfo.PositionMessage)
		[System.Console]::Error.WriteLine($error[0].ScriptStackTrace)
		if ($null -ne $error[0].ErrorDetails) {
			[System.Console]::Error.WriteLine($error[0].ErrorDetails.Message)
		}
		exit 1
	}
}

function Convert-ServiceMessageValue([string]$value)
{
	$valueBytes = [System.Text.Encoding]::UTF8.GetBytes($value)
	return [Convert]::ToBase64String($valueBytes)
}

function Set-OctopusVariable([string]$name, [string]$value, [switch]$sensitive)
{
	$name = Convert-ServiceMessageValue($name)
	$value = Convert-ServiceMessageValue($value)
	$trueEncoded = Convert-ServiceMessageValue("True")

    If ($sensitive) {
        Write-Host "##octopus[setVariable name='$($name)' value='$($value)' sensitive='$($trueEncoded)']"
    } Else {
        Write-Host "##octopus[setVariable name='$($name)' value='$($value)']"
    }
}

function Convert-ToServiceMessageParameter([string]$name, [string]$value)
{
    $value = Convert-ServiceMessageValue($value)
    $param = "$($name)='$($value)'"
	return $param
}

function Remove-OctopusTarget([string] $targetIdOrName)
{
	$targetIdOrName = Convert-ToServiceMessageParameter -name "machine" -value $targetIdOrName
	$parameters = $targetIdOrName -join ' '
	Write-Host "##octopus[delete-target $($parameters)]"
}

function New-OctopusTarget
{
	[CmdletBinding()]
	param(
		[Parameter(ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true, Mandatory=$true)]
		[string]$Name,
		[Parameter(ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true, Mandatory=$true)]
		[string]$TargetId,
		[Parameter(ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true, Mandatory=$true)]
		[string]$Inputs,
		[Parameter(ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true, Mandatory=$true)]
		[string]$Roles,
		[Parameter(ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
		[string]$WorkerPoolIdOrName = "",
		[Parameter(ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
		[switch]$UpdateIfExisting
	)
	process
	{
		$parameters = ""
		$tempParameter = Convert-ToServiceMessageParameter -name "name" -value $Name
		$parameters = $parameters, $tempParameter -join ' '
		$tempParameter = Convert-ToServiceMessageParameter -name "targetId" -value $TargetId
		$parameters = $parameters, $tempParameter -join ' '
		$tempParameter = Convert-ToServiceMessageParameter -name "inputs" -value $Inputs
		$parameters = $parameters, $tempParameter -join ' '
		$tempParameter = Convert-ToServiceMessageParameter -name "octopusRoles" -value $Roles
		$parameters = $parameters, $tempParameter -join ' '
		$tempParameter = Convert-ToServiceMessageParameter -name "octopusDefaultWorkerPoolIdOrName" -value $WorkerPoolIdOrName
		$parameters = $parameters, $tempParameter -join ' '
		$tempParameter = Convert-ToServiceMessageParameter -name "updateIfExisting" -value $UpdateIfExisting
		$parameters = $parameters, $tempParameter -join ' '
		Write-Host "##octopus[createStepPackageTarget $($parameters)]"
	}
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

function New-OctopusArtifact
{
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
        [Alias('fullname')]
        [Alias('path')]
        [string]$fullpath,
        [Parameter(ValueFromPipeline=$true, ValueFromPipelineByPropertyName=$true)]
        [string]$name=""""
    )
    process
    {
	    if ((Test-Path $fullpath) -eq $false) {
		    Write-Verbose "There is no file at '$fullpath' right now. Writing the service message just in case the file is available when the artifacts are collected at a later point in time."
	    }

	    if ($name -eq """")	{
		    $name = [System.IO.Path]::GetFileName($fullpath)
	    }
	    $servicename = Convert-ServiceMessageValue($name)

	    $length = ([System.IO.FileInfo]$fullpath).Length;
	    if (!$length) {
		    $length = 0;
	    }
	    $length = Convert-ServiceMessageValue($length.ToString());

	    $fullpath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($fullpath)
	    $fullpath = [System.IO.Path]::GetFullPath($fullpath)
	    $servicepath = Convert-ServiceMessageValue($fullpath)

	    Write-Verbose "Artifact $name will be collected from $fullpath after this step completes"
	    Write-Host "##octopus[createArtifact path='$($servicepath)' name='$($servicename)' length='$($length)']"
    }
}

function Update-Progress
{
	[CmdletBinding()]
	param(
        [int] $percentage,
        [Parameter(ValueFromPipeline=$true)][string]$message
	)

	process {
        $convertedPercentage = Convert-ServiceMessageValue($percentage)
        $convertedMessage = Convert-ServiceMessageValue($message)
        Write-Host "##octopus[progress percentage='$convertedPercentage' message='$convertedMessage']"
	}
}

function Write-Debug
{
	[CmdletBinding()]
	param([Parameter(ValueFromPipeline=$true)][string]$message)

	begin {}
	process {
		Write-Verbose $message
	}
	end {}
}

function Write-Verbose
{
	[CmdletBinding()]
	param([Parameter(ValueFromPipeline=$true)][string]$message)

	begin {
		Write-Host "##octopus[stdout-verbose]"
	}
	process {
		Write-Host $message
	}
	end {
		Write-Host "##octopus[stdout-default]"
	}
}

function Write-Highlight
{
	[CmdletBinding()]
	param([Parameter(ValueFromPipeline=$true)][string]$message)

	begin {
		Write-Host "##octopus[stdout-highlight]"
	}
	process {
		Write-Host $message
	}
	end {
		Write-Host "##octopus[stdout-default]"
	}
}

function Write-Wait
{
    [CmdletBinding()]
    param([Parameter(ValueFromPipeline=$true)][string]$message)

	begin {
		Write-Host "##octopus[stdout-wait]"
	}
	process {
		Write-Host $message
	}
	end {
		Write-Host "##octopus[stdout-default]"
	}
}

function Write-Warning()
{
	[CmdletBinding()]
	param([Parameter(ValueFromPipeline=$true)][string]$message)

	begin {
		Write-Host "##octopus[stdout-warning]"
	}
	process {
		if($WarningPreference -ne 'SilentlyContinue')
		{
			Write-Host $message
		}
	}
    end {
		Write-Host "##octopus[stdout-default]"
	}
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
	$algorithm.KeySize = 256
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

function Initialize-ProxySettings() {
	$octopusProxyUri = $null
	$proxyUsername = $env:TentacleProxyUsername
	$proxyPassword = $env:TentacleProxyPassword
	$proxyHost = $env:TentacleProxyHost
	[int]$proxyPort = $env:TentacleProxyPort
	if (![string]::IsNullOrEmpty($proxyHost)) {
		$octopusProxyUri = New-Object Uri("http://${proxyHost}:$proxyPort")
	}

	$useDefaultProxy = $true
	if (![string]::IsNullOrEmpty($env:TentacleUseDefaultProxy)) {
		$useDefaultProxy = [System.Convert]::ToBoolean($env:TentacleUseDefaultProxy)
	}

	$useCustomProxy = ![string]::IsNullOrEmpty($proxyHost)
	$hasCredentials = ![string]::IsNullOrEmpty($proxyUsername)

	# This means we should use the HTTP_PROXY variable if it exists, otherwise treat the proxy as not defined
	# TODO: It would probably be better if Calamari was responsible for setting TentacleProxyHost, TentacleProxyUsername etc. based on HTTP_PROXY,
	# but this probably involves resolving https://github.com/OctopusDeploy/Issues/issues/5865 first
	if ($useDefaultProxy -and [string]::IsNullOrEmpty($proxyHost)) {
		# Calamari ensure both http_proxy and HTTP_PROXY are set, so we don't need to worry about casing
		if (![string]::IsNullOrEmpty($env:HTTP_PROXY)) {

			$octopusProxyUri = $null;
			$isUri = [System.Uri]::TryCreate($env:HTTP_PROXY, [System.UriKind]::Absolute, [ref] $octopusProxyUri)
			if (!$isUri) {
				$octopusProxyUri = $env:HTTP_PROXY;
			}

			# The HTTP_PROXY env variable may also contain credentials.
			# This is a common enough pattern, but we need to extract the credentials in order to use them

			# But if credentials were explicitly provided, use those ones instead
			if (-not $hasCredentials -and $octopusProxyUri.GetType() -eq [System.Uri]) {
				$credentialsArray = $octopusProxyUri.UserInfo.Split(":")
				$hasCredentials = $credentialsArray.length -gt 1;
				if ($hasCredentials) {
					$proxyUsername = $credentialsArray[0];
					$proxyPassword = $credentialsArray[1];
				}
			}
		}
	}

	#custom proxy
	if ($useCustomProxy) {
		$proxy = New-Object System.Net.WebProxy($octopusProxyUri)

		if ($hasCredentials) {
			$proxy.Credentials = New-Object System.Net.NetworkCredential($proxyUsername, $proxyPassword)
		}
		else {
			$proxy.Credentials = New-Object System.Net.NetworkCredential("", "")
		}
	}
	else {
		#system proxy
		if ($useDefaultProxy) {
			# The system proxy should be provided through an environment variable, which has been used to initialize $proxyHost
			if ($octopusProxyUri -ne $null) {
				# isWindows was introduced in PSCore and is not available for standard PowerShell
				if (-not (Test-Path variable:IsWindows) -or $isWindows){
					$proxy = [System.Net.WebRequest]::GetSystemWebProxy()
				}
				else {
					$proxy = New-Object System.Net.WebProxy($proxyUri)
				}
			}
			else {
				# If Tentacle is configured to use a System proxy, but there is no system proxy configured then we should configure this as if there was no proxy
				$proxy = New-Object System.Net.WebProxy
			}

			if ($hasCredentials) {
				$proxy.Credentials = New-Object System.Net.NetworkCredential($proxyUsername, $proxyPassword)
			}
			else {
				$proxy.Credentials = [System.Net.CredentialCache]::DefaultCredentials
			}
		}
		#bypass proxy
		else {
			$proxy = New-Object System.Net.WebProxy
		}
	}

	# In some versions of PowerShell Core (pre version 7.0.0), if a script uses HttpClient
	# it won't be automatically be configured with the right proxy.
	# This is unavoidable for those versions, see See https://github.com/dotnet/corefx/issues/36553 for more information
	# We expose an $OctopusProxy variable that can be used to manually configure HttpClient instances, and this variable is documented
	# For consistency, we expose this variable across all versions of powershell, in case users have other usages of this variable
	$global:OctopusProxy = $proxy
	[System.Net.WebRequest]::DefaultWebProxy = $proxy

	if ($PSVersionTable.PSEdition -eq "Core") {
		if ($PSVersionTable.PsVersion.Major -lt 7) {
			# HttpClient is used to implement built in cmdlets like Invoke-WebRequest.
			# Because earlier versions of PowerShell Core don't allow us to set a default proxy for HttpClient,
			# the cmdlets also don't get a default proxy set either
			# Fortunately, for these cmdlets we can use $PSDefaultParameterValues to provide the right defaults
			# We don't use default parameter values in Windows PowerShell because this simplifies things,
			# and means that users could change this value globally by modifying just a single property
			if ($useDefaultProxy -or $useCustomProxy) {
				if ($octopusProxyUri -ne $null) {
					$PSDefaultParameterValues.Add("Invoke-WebRequest:Proxy", $octopusProxyUri.ToString())
					$PSDefaultParameterValues.Add("Invoke-RestMethod:Proxy", $octopusProxyUri.ToString())
					if ($hasCredentials) {
						$securePassword = ConvertTo-SecureString $proxyPassword -AsPlainText -Force
						$credentials = New-Object System.Management.Automation.PSCredential -ArgumentList $proxyUsername, $securePassword

						$PSDefaultParameterValues.Add("Invoke-WebRequest:ProxyCredential", $credentials)
						$PSDefaultParameterValues.Add("Invoke-RestMethod:ProxyCredential", $credentials)
					}
				}
			}
		}
		else {
			# In versions of PowerShell Core after 7.0.0, there is a mechanism for setting a default proxy on HttpClient
			# See https://github.com/dotnet/corefx/pull/37333
			[System.Net.Http.HttpClient]::DefaultProxy = $proxy
		}
	}
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
				$moduleImportMeasurement = Measure-Command -Expression {Import-Module –Name $calamariModulePath}
				Write-Verbose "Spent $($moduleImportMeasurement.TotalSeconds) seconds importing the Powershell module $calamariModulePath"
		    } else {
        		$env:PSModulePath = $calamariModulePath + ";" + $env:PSModulePath
		    }
		}
	}
}

function ConvertTo-QuotedString([string]$arg){
	"`"$arg`""
}

function ConvertTo-ConsoleEscapedArgument([string]$arg){
    ## For all ", double the number of \ immediately preceding the "
    $arg = $arg -replace "(\\+)+`"",'$1$1"'

    ## Add a single \ preceding all "
    $arg = $arg.Replace("`"", "\`"");

    ## if string ends with \, double the number of all ending \
    $arg = $arg -replace "(\\+)+$",'$1$1"'

    return $arg
}

function ConvertTo-PowershellEscapedArgument([string]$arg){
	## Escape all ` with another ` (this is needed so PS does not get confused with variables)
    $arg = $arg.Replace("``", "````");

	## Escape all [ with a ` (this is needed so PS does not get confused with variables)
    $arg = $arg.Replace("[", "``[");

    ## Escape all $ with a ` (this is needed so PS does not get confused with variables)
    $arg = $arg.Replace("$", "`$");

    return $arg
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
try
{
	. '{{TargetScriptFile}}' {{ScriptParameters}}
}
catch
{

	[System.Console]::Error.WriteLine("$($error[0].CategoryInfo.Category): $($error[0].Exception.Message)")
	[System.Console]::Error.WriteLine($error[0].InvocationInfo.PositionMessage)
	[System.Console]::Error.WriteLine($error[0].ScriptStackTrace)
	if ($null -ne $error[0].ErrorDetails) {
		[System.Console]::Error.WriteLine($error[0].ErrorDetails.Message)
	}

	exit 1
}

# -----------------------------------------------------------------
# Ensure we exit with whatever exit code the last exe used
# -----------------------------------------------------------------

if ((test-path variable:global:lastexitcode))
{
	exit $LastExitCode
}
