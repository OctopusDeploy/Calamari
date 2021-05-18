$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -lt 5)
{
    throw "These Azure commands are only supported in PowerShell versions 5 and above. This server is currently running PowerShell version $($PSVersionTable.PSVersion.ToString())."
}

function EnsureDirectoryExists([string] $path)
{
    New-Item -ItemType Directory -Force -Path $path *>$null
}

function SetupExecutable() {
	$custom_exe=$OctopusParameters["Octopus.Action.GoogleCloud.CustomExecutable"]
	if ([string]::IsNullOrEmpty($custom_exe)) {
	    if ((Get-Command gcloud -ErrorAction SilentlyContinue) -eq $null) {
            Write-Error "Could not find gcloud. Make sure gcloud is on the PATH."
            Exit 1
        }
	} else {
		$custom_exe_exists = Test-Path $custom_exe -PathType Leaf
		if(-not $custom_exe_exists) {
			Write-Error "The custom gcloud location of $custom_exe does not exist. Please make sure gcloud in installed in that location."
			Exit 1
		}
		Set-Alias -Name gcloud -Value $custom_exe_exists
	}
}

SetupExecutable

pushd $env:OctopusCalamariWorkingDirectory
try {
    try {
        Write-Host "##octopus[stdout-verbose]"

        $env:CLOUDSDK_CONFIG = [System.IO.Path]::Combine($env:OctopusCalamariWorkingDirectory, "gcloud-cli")
        EnsureDirectoryExists($env:CLOUDSDK_CONFIG)

        gcloud version
        Write-Verbose "Google Cloud CLI: Authenticating with key file"
        $loginArgs = @();
        $loginArgs += @("--key-file=$(ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusGoogleCloudKeyFile)))");

        Write-Host "gcloud auth activate-service-account $loginArgs"
        gcloud auth activate-service-account $loginArgs

        Write-Host "##octopus[stdout-default]"
        Write-Verbose "Successfully authenticated with Google Cloud CLI"
    } catch  {
        # failed to authenticate with Azure CLI
        Write-Verbose "Failed to authenticate with Google Cloud CLI"
        Write-Verbose $_.Exception.Message
    }
}
finally {
    popd
}

Write-Verbose "Invoking target script $OctopusGoogleCloudTargetScript with $OctopusGoogleCloudTargetScriptParameters parameters"

try {
    Invoke-Expression ". `"$OctopusGoogleCloudTargetScript`" $OctopusGoogleCloudTargetScriptParameters"
}  finally {
    try {
        # Save the last exit code so doesn't clobber it
        $previousLastExitCode = $LastExitCode
        $previousErrorAction = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        gcloud auth revoke --all 2>$null 3>$null
    } finally {
        # restore the previous last exit code
        $LastExitCode = $previousLastExitCode
        $ErrorActionPreference = $previousErrorAction
    }
}
