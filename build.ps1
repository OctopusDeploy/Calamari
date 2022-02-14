##########################################################################
# This is the Cake bootstrapper script for PowerShell.
# This file was downloaded from https://github.com/cake-build/resources
# Feel free to change this file to fit your needs.
##########################################################################

<#

.SYNOPSIS
This is a Powershell script to bootstrap a Cake build.

.DESCRIPTION
This Powershell script will download NuGet if missing, restore NuGet tools (including Cake)
and execute your Cake build script with the parameters you provide.

.PARAMETER Script
The build script to execute.
.PARAMETER Target
The build script target to run.
.PARAMETER Configuration
The build configuration to use.
.PARAMETER Verbosity
Specifies the amount of information to be displayed.
.PARAMETER Experimental
Tells Cake to use the latest Roslyn release.
.PARAMETER WhatIf
Performs a dry run of the build script.
No tasks will be executed.
.PARAMETER Mono
Tells Cake to use the Mono scripting engine.
.PARAMETER SkipToolPackageRestore
Skips restoring of packages.
.PARAMETER BuildVerbosity
Specifies the verbosity of any msbuild or dotnet build actions (default = Normal).
.PARAMETER $PackInParallel
Specifies whether to perform any pack actions in parallel (default = false)
.PARAMETER $Timestamp
Specifies whether to append the current timestamp to the version (default = false)
.PARAMETER $SetOctopusServerVersion
Specifies whether to set the Octopus Server version as part of the build (default = false)
.PARAMETER $SignFilesOnLocalBuild
Specifies whether to sign the files as part of a local build (default = false)
Files are always signed on server builds.
.PARAMETER ScriptArgs
Remaining arguments are added here.

.LINK
http://cakebuild.net

#>

[CmdletBinding()]
Param(
    [string]$Target = "Default",
    [string]$Script = "build.cake",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [string]$Where = ".",
	[ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity = "Verbose",
    [switch]$Experimental = $true,
    [Alias("DryRun","Noop")]
    [switch]$WhatIf,
    [switch]$Mono,
    [switch]$SkipToolPackageRestore,
    [string]$SigningCertificatePath = "./certificates/OctopusDevelopment.pfx",
    [string]$SigningCertificatePassword = "Password01!",
    [string]$AzureKeyVaultUrl = " ",
    [string]$AzureKeyVaultAppId = " ",
    [string]$AzureKeyVaultAppSecret = " ",
    [string]$AzureKeyvaultCertificateName = " ",
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$BuildVerbosity = "Normal",
    [switch]$PackInParallel,
    [switch]$Timestamp,
    [switch]$SetOctopusServerVersion,
    [switch]$SignFilesOnLocalBuild
)

Write-Host "Preparing to run build script..."

# Should we use mono?
$UseMono = "";
if($Mono.IsPresent) {
    Write-Verbose -Message "Using the Mono based scripting engine."
    $UseMono = "--mono"
}

# Should we use the new Roslyn?
$UseExperimental = "";
if($Experimental.IsPresent -and !($Mono.IsPresent)) {
    Write-Verbose -Message "Using experimental version of Roslyn."
    $UseExperimental = "--experimental"
}

# Is this a dry run?
$UseDryRun = "";
if($WhatIf.IsPresent) {
    $UseDryRun = "--dryrun"
}

# Should we run pack operations in parallel
$UsePackInParallel = ""
if ($PackInParallel.IsPresent) {
    $UsePackInParallel = "--packinparallel=true"
}

# Should we append a timestamp to the version
$UseTimestamp = ""
if ($Timestamp.IsPresent) {
    $UseTimestamp = "--timestamp=true"
}

# Should we set the octopus server version
$UseSetOctopusServerVersion = ""
if ($SetOctopusServerVersion.IsPresent) {
    $UseSetOctopusServerVersion = "--setoctopusserverversion=true"
}

# Should we sign the files if we're building locally
$UseSignFilesOnLocalBuild = ""
if ($SignFilesOnLocalBuild.IsPresent) {
    $UseSignFilesOnLocalBuild = "--signFiles=true"
}

# Make sure that Cake and other tools have been installed
dotnet tool install --global Cake.Tool
dotnet tool install --global GitVersion.Tool
dotnet tool install --global azuresigntool

# We added this so we can use dotnet tools
# See https://www.gep13.co.uk/blog/introducing-cake.dotnettool.module
Write-Host "Installing cake modules using the --bootstrap argument"
dotnet-cake --bootstrap

if ($LASTEXITCODE -eq 0)
{
    # Start Cake
    Write-Host "Running build script:"
    Write-Host "dotnet-cake `"$Script`" --target=`"$Target`" --configuration=`"$Configuration`" --verbosity=`"$Verbosity`" --signingCertificatePath=`"$SigningCertificatePath`" --signingCertificatePassword=`"$SigningCertificatePassword`" --AzureKeyVaultUrl=`"$AzureKeyVaultUrl`" --AzureKeyVaultAppId=`"$AzureKeyVaultAppId`" --AzureKeyVaultAppSecret=`"$AzureKeyVaultAppSecret`" --AzureKeyvaultCertificateName=`"$AzureKeyvaultCertificateName`" --buildVerbosity=`"$BuildVerbosity`" $UseMono $UseDryRun $UseExperimental $UsePackInParallel $UseTimestamp $UseSetOctopusServerVersion $UseSignFilesOnLocalBuild"

    Invoke-Expression "dotnet-cake `"$Script`" --target=`"$Target`" --configuration=`"$Configuration`" --verbosity=`"$Verbosity`" --signingCertificatePath=`"$SigningCertificatePath`" --signingCertificatePassword=`"$SigningCertificatePassword`" --AzureKeyVaultUrl=`"$AzureKeyVaultUrl`" --AzureKeyVaultAppId=`"$AzureKeyVaultAppId`" --AzureKeyVaultAppSecret=`"$AzureKeyVaultAppSecret`" --AzureKeyvaultCertificateName=`"$AzureKeyvaultCertificateName`" --buildVerbosity=`"$BuildVerbosity`" $UseMono $UseDryRun $UseExperimental $UsePackInParallel $UseTimestamp $UseSetOctopusServerVersion $UseSignFilesOnLocalBuild"
}

exit $LASTEXITCODE
