
function Get-ExecutionVariable([string]$name)
{
    return $OctopusParameters["Octopus.Action.Azure.PowerShellExecution.$name"]
}

$SourcePath = Get-ExecutionVariable 'SourcePath'
$DestinationPath = Get-ExecutionVariable 'DestinationPath'
$DestinationUserName = Get-ExecutionVariable 'DestinationUserName'
$DestinationPassword = Get-ExecutionVariable 'DestinationPassword'
$DestinationUri = Get-ExecutionVariable 'DestinationUri'
$DestinationDeploymentSite = Get-ExecutionVariable 'DestinationDeploymentSite'
$UseChecksum = (Get-ExecutionVariable 'UseChecksum') -eq 'True'
$DoNotDelete = (Get-ExecutionVariable 'DoNotDelete') -eq 'True'
$UseAppOffline = (Get-ExecutionVariable 'UseAppOffline') -eq 'True'
$PreserveAppData = (Get-ExecutionVariable 'PreserveAppData') -eq 'True'
$PreservePathsPiped = Get-ExecutionVariable 'PreservePathsPiped'

$PreservePaths = $PreservePathsPiped.Split('|')

$ErrorActionPreference = 'Stop'
$WebDeploymentPackageVersion = '4.0.5' # matches the version Calamari.AzureWebApp.NetCoreShim references

function Get-DownloadedWebDeploymentDllPath
{
    $cacheDir = Join-Path $env:LOCALAPPDATA "Octopus\MicrosoftWebDeployment\$WebDeploymentPackageVersion"
    $cachedDllPath = Join-Path $cacheDir "Microsoft.Web.Deployment.dll"

    if (Test-Path -LiteralPath $cachedDllPath)
    {
        return $cachedDllPath
    }

    Write-Warning ("Microsoft.Web.Deployment.dll was not found locally (no override supplied, Web Deploy doesn't " +
            "appear to be installed on this machine). Downloading it from nuget.org instead - this is not " +
            "ideal for a repeatable deployment. Install Web Deploy, or set the " +
            "Octopus.Action.Azure.MicrosoftWebDeploymentDllPath variable, to avoid this network dependency.")

    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
    $nupkgPath = Join-Path $cacheDir "microsoft.web.deployment.$WebDeploymentPackageVersion.nupkg"
    $downloadUri = "https://api.nuget.org/v3-flatcontainer/microsoft.web.deployment/$WebDeploymentPackageVersion/microsoft.web.deployment.$WebDeploymentPackageVersion.nupkg"

    Invoke-WebRequest -Uri $downloadUri -OutFile $nupkgPath -UseBasicParsing

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $extractDir = Join-Path $cacheDir "extracted"
    if (Test-Path -LiteralPath $extractDir) { Remove-Item -Recurse -Force $extractDir }
    [System.IO.Compression.ZipFile]::ExtractToDirectory($nupkgPath, $extractDir)

    $extractedDllPath = Join-Path $extractDir "lib\net45\Microsoft.Web.Deployment.dll"
    if (-not (Test-Path -LiteralPath $extractedDllPath))
    {
        throw "Downloaded Microsoft.Web.Deployment $WebDeploymentPackageVersion from nuget.org but could not find $extractedDllPath inside it."
    }

    Copy-Item -LiteralPath $extractedDllPath -Destination $cachedDllPath -Force
    Remove-Item -Recurse -Force $extractDir, $nupkgPath -ErrorAction SilentlyContinue

    return $cachedDllPath
}

function Resolve-WebDeploymentDllPath
{
    # 1. An explicit candidate - either the customer's own override variable, or (for the Phase 1
    #    Calamari.AzureWebApp path specifically) the copy already bundled for the netcoreshim. Not
    #    trusted unconditionally: verified below like every other candidate, so a stale or wrong path
    #    falls through to the next option instead of failing outright.
    if ($MicrosoftWebDeploymentDllPath -and (Test-Path -LiteralPath $MicrosoftWebDeploymentDllPath))
    {
        return $MicrosoftWebDeploymentDllPath
    }

    # 2. Locally installed Web Deploy.
    $wellKnownPaths = @(
        "$env:ProgramFiles\IIS\Microsoft Web Deploy V3\Microsoft.Web.Deployment.dll",
        "${env:ProgramFiles(x86)}\IIS\Microsoft Web Deploy V3\Microsoft.Web.Deployment.dll"
    )
    foreach ($path in $wellKnownPaths)
    {
        if (Test-Path -LiteralPath $path)
        {
            return $path
        }
    }

    # 3. Last resort - download it. Only reached when nothing local was found.
    return Get-DownloadedWebDeploymentDllPath
}

Add-Type -Path (Resolve-WebDeploymentDllPath)

# --- DeploymentBaseOptions (mirrors WebDeploymentExecutor.DeploymentOptions) ---
$deploymentOptions = New-Object Microsoft.Web.Deployment.DeploymentBaseOptions
$deploymentOptions.AuthenticationType = 'Basic'
$deploymentOptions.RetryAttempts = 3
$deploymentOptions.RetryInterval = 1000
$deploymentOptions.TraceLevel = [System.Diagnostics.TraceLevel]::Verbose
$deploymentOptions.UserName = $DestinationUserName
$deploymentOptions.Password = $DestinationPassword
$deploymentOptions.UserAgent = 'OctopusDeploy/1.0'
# Uses the publish profile's own msdeploySite value (matches classic's WebDeployPublishSettings.DeploymentSite),
# not a hand-rolled "$SiteName-$SlotName" - Azure's actual slot-site naming convention isn't a plain hyphen join,
# so building it locally risked targeting the wrong slot's msdeploy.axd endpoint.
$deploymentOptions.ComputerName = "$DestinationUri/msdeploy.axd?site=$DestinationDeploymentSite"

# Verified on Windows: add_Trace() binds without error, but Web Deploy raises the Trace event from
# its own native callback thread, which has no PowerShell Runspace attached - invoking the script
# block there throws "There is no Runspace available to run scripts in this thread." at SyncTo time.
# Register-ObjectEvent avoids this: it queues the event onto the engine's own event subsystem, which
# does have a Runspace, at the cost of the handler running asynchronously relative to SyncTo().
Register-ObjectEvent -InputObject $deploymentOptions -EventName Trace -Action {
    $traceArgs = $EventArgs
    switch ($traceArgs.EventLevel)
    {
        ([System.Diagnostics.TraceLevel]::Verbose) { Write-Verbose $traceArgs.Message }
        ([System.Diagnostics.TraceLevel]::Info)     { Write-Verbose $traceArgs.Message } # deploy-log is noisy, same as today
        ([System.Diagnostics.TraceLevel]::Warning)  { Write-Warning $traceArgs.Message }
        ([System.Diagnostics.TraceLevel]::Error)    { Write-Error $traceArgs.Message }
    }
} | Out-Null

# --- DeploymentSyncOptions (mirrors WebDeploymentExecutor.DeploymentSyncOptions) ---
$syncOptions = New-Object Microsoft.Web.Deployment.DeploymentSyncOptions
$syncOptions.WhatIf = $false
$syncOptions.UseChecksum = [bool]$UseChecksum
$syncOptions.DoNotDelete = [bool]$DoNotDelete

if ($UseAppOffline)
{
    # NEEDS VERIFYING ON WINDOWS: TryGetValue via [ref] out-parameter syntax.
    $rules = [Microsoft.Web.Deployment.DeploymentSyncOptions]::GetAvailableRules()
    $appOfflineRule = $null
    if ($rules.TryGetValue('AppOffline', [ref]$appOfflineRule))
    {
        $syncOptions.Rules.Add($appOfflineRule)
    }
    else
    {
        Write-Verbose 'Azure Deployment API does not support `AppOffline` deployment rule.'
    }
}

if ($PreserveAppData)
{
    $syncOptions.Rules.Add((New-Object Microsoft.Web.Deployment.DeploymentSkipRule('SkipDeleteDataFiles', 'Delete', 'filePath', '\\App_Data\\.*', $null)))
    $syncOptions.Rules.Add((New-Object Microsoft.Web.Deployment.DeploymentSkipRule('SkipDeleteDataDir', 'Delete', 'dirPath', '\\App_Data(\\.*|$)', $null)))
}

for ($i = 0; $i -lt $PreservePaths.Count; $i++)
{
    $path = $PreservePaths[$i]
    $syncOptions.Rules.Add((New-Object Microsoft.Web.Deployment.DeploymentSkipRule("SkipDeleteFiles_$i", 'Delete', 'filePath', $path, $null)))
    $syncOptions.Rules.Add((New-Object Microsoft.Web.Deployment.DeploymentSkipRule("SkipDeleteDir_$i", 'Delete', 'dirPath', $path, $null)))
}

# --- Retry (mirrors WebDeploymentExecutor.Execute's inner RetryTracker: 4 tries, 5s-30s exponential) ---
$maxAttempts = 4
$attempt = 0
$delayMs = 5000

while ($true)
{
    $attempt++
    try
    {
        $changeSummary = [Microsoft.Web.Deployment.DeploymentManager]::CreateObject('contentPath', $SourcePath).SyncTo(
                'contentPath', $DestinationPath, $deploymentOptions, $syncOptions)

        Write-Host "Successfully deployed to Azure. $($changeSummary.ObjectsAdded) objects added. $($changeSummary.ObjectsUpdated) objects updated. $($changeSummary.ObjectsDeleted) objects deleted."
        break
    }
    catch [Microsoft.Web.Deployment.DeploymentDetailedException]
    {
        if ($attempt -ge $maxAttempts)
        {
            throw
        }

        Write-Verbose "Retry #$attempt on Azure deploy. Exception: $($_.Exception.Message)"
        Start-Sleep -Milliseconds $delayMs
        $delayMs = [Math]::Min($delayMs * 2, 30000)
    }
}
