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
    [string]$Where = "",
	[ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity = "Verbose",
    [switch]$Experimental = $true,
    [Alias("DryRun","Noop")]
    [switch]$WhatIf,
    [switch]$Mono,
    [switch]$SkipToolPackageRestore,
    [string]$AzureKeyVaultUrl,
    [string]$AzureKeyVaultAppId,
    [string]$AzureKeyVaultAppSecret,
    [string]$AzureKeyvaultCertificateName,
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$BuildVerbosity = "Normal",
    [switch]$PackInParallel,
    [switch]$Timestamp,
    [switch]$SetOctopusServerVersion,
    [switch]$SignFilesOnLocalBuild
)

[Reflection.Assembly]::LoadWithPartialName("System.Security") | Out-Null
function MD5HashFile([string] $filePath)
{
    if ([string]::IsNullOrEmpty($filePath) -or !(Test-Path $filePath -PathType Leaf))
    {
        return $null
    }

    [System.IO.Stream] $file = $null;
    [System.Security.Cryptography.MD5] $md5 = $null;
    try
    {
        $md5 = [System.Security.Cryptography.MD5]::Create()
        $file = [System.IO.File]::OpenRead($filePath)
        return [System.BitConverter]::ToString($md5.ComputeHash($file))
    }
    finally
    {
        if ($file -ne $null)
        {
            $file.Dispose()
        }
    }
}

Write-Host "Preparing to run build script..."

if(!$PSScriptRoot){
    $PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent
}

$TOOLS_DIR = Join-Path $PSScriptRoot "tools"
$ARTIFACTS_DIR = Join-Path $PSScriptRoot "artifacts"
$ADDINS_DIR = Join-Path $TOOLS_DIR "addins"
$MODULES_DIR = Join-Path $TOOLS_DIR "modules"
$NUGET_EXE = Join-Path $TOOLS_DIR "nuget.exe"
$CAKE_EXE = Join-Path $TOOLS_DIR "Cake/Cake.exe"
$NUGET_URL = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
$PACKAGES_CONFIG = Join-Path $TOOLS_DIR "packages.config"
$PACKAGES_CONFIG_MD5 = Join-Path $TOOLS_DIR "packages.config.md5sum"
$ADDINS_PACKAGES_CONFIG = Join-Path $ADDINS_DIR "packages.config"
$MODULES_PACKAGES_CONFIG = Join-Path $MODULES_DIR "packages.config"

# Should we use mono?
$UseMono = "";
if($Mono.IsPresent) {
    Write-Verbose -Message "Using the Mono based scripting engine."
    $UseMono = "-mono"
}

# Should we use the new Roslyn?
$UseExperimental = "";
if($Experimental.IsPresent -and !($Mono.IsPresent)) {
    Write-Verbose -Message "Using experimental version of Roslyn."
    $UseExperimental = "-experimental"
}

# Is this a dry run?
$UseDryRun = "";
if($WhatIf.IsPresent) {
    $UseDryRun = "-dryrun"
}

# Should we run pack operations in parallel
$UsePackInParallel = ""
if ($PackInParallel.IsPresent) {
    $UsePackInParallel = "-packinparallel=true"
}

# Should we append a timestamp to the version
$UseTimestamp = ""
if ($Timestamp.IsPresent) {
    $UseTimestamp = "-timestamp=true"
}

# Should we set the octopus server version
$UseSetOctopusServerVersion = ""
if ($SetOctopusServerVersion.IsPresent) {
    $UseSetOctopusServerVersion = "-setoctopusserverversion=true"
}

# Should we sign the files if we're building locally
$UseSignFilesOnLocalBuild = ""
if ($SignFilesOnLocalBuild.IsPresent) {
    $UseSignFilesOnLocalBuild = "-sign_files=true"
}

# Make sure tools folder exists
if ((Test-Path $PSScriptRoot) -and !(Test-Path $TOOLS_DIR)) {
    Write-Verbose -Message "Creating tools directory..."
    New-Item -Path $TOOLS_DIR -Type directory | out-null
}

# Make sure artifacts folder exists
if ((Test-Path $PSScriptRoot) -and !(Test-Path $ARTIFACTS_DIR)) {
    Write-Verbose -Message "Creating artifacts directory..."
    New-Item -Path $ARTIFACTS_DIR -Type directory | out-null
}

# Make sure that packages.config exist.
if (!(Test-Path $PACKAGES_CONFIG)) {
    Write-Verbose -Message "Downloading packages.config..."
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    try { (New-Object System.Net.WebClient).DownloadFile("https://cakebuild.net/download/bootstrapper/packages", $PACKAGES_CONFIG) } catch {
        Throw "Could not download packages.config."
    }
}

# Try find NuGet.exe in path if not exists
if (!(Test-Path $NUGET_EXE)) {
    Write-Verbose -Message "Trying to find nuget.exe in PATH..."
    $existingPaths = $Env:Path -Split ';' | Where-Object { (![string]::IsNullOrEmpty($_)) -and (Test-Path $_ -PathType Container) }
    $NUGET_EXE_IN_PATH = Get-ChildItem -Path $existingPaths -Filter "nuget.exe" | Select -First 1
    if ($NUGET_EXE_IN_PATH -ne $null -and (Test-Path $NUGET_EXE_IN_PATH.FullName)) {
        Write-Verbose -Message "Found in PATH at $($NUGET_EXE_IN_PATH.FullName)."
        $NUGET_EXE = $NUGET_EXE_IN_PATH.FullName
    }
}

# Try download NuGet.exe if not exists
if (!(Test-Path $NUGET_EXE)) {
    Write-Verbose -Message "Downloading NuGet.exe from $NUGET_URL to $NUGET_EXE..."
    try {
        (New-Object System.Net.WebClient).DownloadFile($NUGET_URL, $NUGET_EXE)
    } catch {
        Throw "Could not download NuGet.exe from $NUGET_URL to $NUGET_EXE " + $_.Exception.Message
    }
}

# Save nuget.exe path to environment to be available to child processed
$ENV:NUGET_EXE = $NUGET_EXE

# Restore tools from NuGet?
if(-Not $SkipToolPackageRestore.IsPresent) {
    Push-Location
    Set-Location $TOOLS_DIR

    # Check for changes in packages.config and remove installed tools if true.
    [string] $md5Hash = MD5HashFile($PACKAGES_CONFIG)
    if((!(Test-Path $PACKAGES_CONFIG_MD5)) -Or
      ($md5Hash -ne (Get-Content $PACKAGES_CONFIG_MD5 ))) {
        Write-Verbose -Message "Missing or changed package.config hash..."
        Remove-Item * -Recurse -Exclude packages.config,nuget.exe
    }

    Write-Verbose -Message "Restoring tools from NuGet..."
    $NuGetOutput = Invoke-Expression "&`"$NUGET_EXE`" install -ExcludeVersion -OutputDirectory `"$TOOLS_DIR`""

    if ($LASTEXITCODE -ne 0) {
        Throw "An error occured while restoring NuGet tools."
    }
    else
    {
        $md5Hash | Out-File $PACKAGES_CONFIG_MD5 -Encoding "ASCII"
    }
    Write-Verbose -Message ($NuGetOutput | out-string)
    
    Pop-Location
}

# Restore addins from NuGet
if (Test-Path $ADDINS_PACKAGES_CONFIG) {
    Push-Location
    Set-Location $ADDINS_DIR

    Write-Verbose -Message "Restoring addins from NuGet..."
    $NuGetOutput = Invoke-Expression "&`"$NUGET_EXE`" install -ExcludeVersion -OutputDirectory `"$ADDINS_DIR`""

    if ($LASTEXITCODE -ne 0) {
        Throw "An error occured while restoring NuGet addins."
    }

    Write-Verbose -Message ($NuGetOutput | out-string)

    Pop-Location
}

# Restore modules from NuGet
if (Test-Path $MODULES_PACKAGES_CONFIG) {
    Push-Location
    Set-Location $MODULES_DIR

    Write-Verbose -Message "Restoring modules from NuGet..."
    $NuGetOutput = Invoke-Expression "&`"$NUGET_EXE`" install -ExcludeVersion -OutputDirectory `"$MODULES_DIR`""

    if ($LASTEXITCODE -ne 0) {
        Throw "An error occured while restoring NuGet modules."
    }

    Write-Verbose -Message ($NuGetOutput | out-string)

    Pop-Location
}

# Make sure that Cake has been installed.
if (!(Test-Path $CAKE_EXE)) {
    Throw "Could not find Cake.exe at $CAKE_EXE"
}

# We added this so we can use dotnet tools
# See https://www.gep13.co.uk/blog/introducing-cake.dotnettool.module
Write-Host "Installing cake modules using the --bootstrap argument"
&$CAKE_EXE --bootstrap

# Start Cake
Write-Host "Running build script..."
Invoke-Expression "& `"$CAKE_EXE`" `"$Script`" -target=`"$Target`" -configuration=`"$Configuration`" -verbosity=`"$Verbosity`" -where=`"$Where`" -signing_certificate_path=`"$SigningCertificatePath`" -signing_certificate_password=`"$SigningCertificatePassword`" -build_verbosity=`"$BuildVerbosity`" -AzureKeyVaultUrl=`"$AzureKeyVaultUrl`" -AzureKeyVaultAppId=`"$AzureKeyVaultAppId`" -AzureKeyVaultAppSecret=`"$AzureKeyVaultAppSecret`" -AzureKeyvaultCertificateName=`"$AzureKeyvaultCertificateName`" $UseMono $UseDryRun $UseExperimental $UsePackInParallel $UseTimestamp $UseSetOctopusServerVersion $UseSignFilesOnLocalBuild"
exit $LASTEXITCODE
