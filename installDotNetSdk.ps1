Write-Output "PowerShell $($PSVersionTable.PSEdition) version $($PSVersionTable.PSVersion)"

Set-StrictMode -Version 2.0; $ErrorActionPreference = "Stop"; $ConfirmPreference = "None"; trap { Write-Error $_ -ErrorAction Continue; exit 1 }
$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent

###########################################################################
# CONFIGURATION
###########################################################################

$TempDirectory = "$PSScriptRoot\\.nuke\temp"
$DotNetGlobalFile = "$PSScriptRoot\\global.json"
$DotNetInstallUrl = "https://dot.net/v1/dotnet-install.ps1"
$DotNetChannel = "Current"

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 1
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:DOTNET_MULTILEVEL_LOOKUP = 0

###########################################################################
# EXECUTION
###########################################################################

function ExecSafe([scriptblock] $cmd) {
    & $cmd
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
}

# Download install script
$DotNetInstallFile = "$TempDirectory\dotnet-install.ps1"
New-Item -ItemType Directory -Path $TempDirectory -Force | Out-Null
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
(New-Object System.Net.WebClient).DownloadFile($DotNetInstallUrl, $DotNetInstallFile)

# If global.json exists, load expected version
if (Test-Path $DotNetGlobalFile) {
    $DotNetGlobal = $(Get-Content $DotNetGlobalFile | Out-String | ConvertFrom-Json)
    if ($DotNetGlobal.PSObject.Properties["sdk"] -and $DotNetGlobal.sdk.PSObject.Properties["version"]) {
        $DotNetVersion = $DotNetGlobal.sdk.version
    }
}

Remove-Variable DotNetVersion
$DotNetChannel = "8.0"

# Install by channel or version
$DotNetDirectory = "$TempDirectory\dotnet-win"
if (!(Test-Path variable:DotNetVersion)) {
    ExecSafe { & powershell $DotNetInstallFile -Channel $DotNetChannel }
} else {
    ExecSafe { & powershell $DotNetInstallFile -Version $DotNetVersion }
}

[Environment]::SetEnvironmentVariable("Path", "$($env:Path);%LocalAppData%\Microsoft\dotnet", [System.EnvironmentVariableTarget]::Machine)
