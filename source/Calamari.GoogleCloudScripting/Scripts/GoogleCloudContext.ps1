$ErrorActionPreference = "Stop"

function Get-Executable() {
	$gcloud_exe=$OctopusParameters["OctopusGCloudExe"]
    if ((Get-Command $gcloud_exe -ErrorAction SilentlyContinue) -eq $null) {
        Write-Error "Could not find $gcloud_exe. Make sure $gcloud_exe is on the PATH."
        Exit 1
    }

	return $gcloud_exe;
}

$gcloud_exe = Get-Executable

pushd $env:OctopusCalamariWorkingDirectory
try {
    try {
        Write-Host "##octopus[stdout-verbose]"

        & $gcloud_exe version
        Write-Verbose "Google Cloud CLI: Authenticating with key file"
        $loginArgs = @();
        $loginArgs += @("--key-file=$(ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusGoogleCloudKeyFile)))");

        Write-Host "gcloud auth activate-service-account $loginArgs"
        & $gcloud_exe auth activate-service-account $loginArgs

        Write-Host "##octopus[stdout-default]"
        Write-Verbose "Successfully authenticated with Google Cloud CLI"
    } catch  {
        # failed to authenticate with Azure CLI
        Write-Verbose "Failed to authenticate with Google Cloud CLI"
        Write-Verbose $_.Exception.Message
        Exit 1
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
        & $gcloud_exe auth revoke --all 2>$null 3>$null
    } finally {
        # restore the previous last exit code
        $LastExitCode = $previousLastExitCode
        $ErrorActionPreference = $previousErrorAction
    }
}
