## Octopus Azure Service Fabric Application script, version 1.0
## --------------------------------------------------------------------------------------
##
## This script is used to control how we deploy packages to Azure Service Fabric applications to a cluster. 
##
## This is a modified version of the Visual Studio's Service Fabric 'Deploy-FabricApplication.ps1' file. This version includes automatic support for 'fresh install vs upgrade' scenarios.
## Thx to Colin Dembovsky @ http://colinsalmcorner.com/post/continuous-deployment-of-service-fabric-apps-using-vsts-or-tfs for posting about this.
##
## The script will be passed the following parameters in addition to the normal Octopus 
## variables passed to any PowerShell script.
##
##   OctopusAzureFabricConnectionEndpoint                               // The connection endpoint
##   OctopusAzureFabricPublishProfileFile                               // Path to the file containing the publish profile.
##   OctopusAzureFabricApplicationPackagePath                           // Path to the folder of the packaged Service Fabric application.
##   OctopusAzureFabricDeployOnly                                       // Indicates that the Service Fabric application should not be created or upgraded after registering the application type.
##   OctopusAzureFabricApplicationParameters                            // Hashtable of the Service Fabric application parameters to be used for the application.
##   OctopusAzureFabricUnregisterUnusedApplicationVersionsAfterUpgrade  // Indicates whether to unregister any unused application versions that exist after an upgrade is finished.
##   OctopusAzureFabricOverrideUpgradeBehavior                          // Indicates the behavior used to override the upgrade settings specified by the publish profile. Options: None | ForceUpgrade | VetoUpgrade
##   OctopusAzureFabricUseExistingClusterConnection                     // Indicates that the script should make use of an existing cluster connection that has already been established in the PowerShell session.  The cluster connection parameters configured in the publish profile are ignored.
##   OctopusAzureFabricOverwriteBehavior                                // Overwrite Behavior if an application exists in the cluster with the same name. Available Options are Never, Always, SameAppTypeAndVersion. This setting is not applicable when upgrading an application.
##   OctopusAzureFabricSkipPackageValidation                            // Switch signaling whether the package should be validated or not before deployment.
##   OctopusAzureFabricSecurityToken                                    // A security token for authentication to cluster management endpoints. Used for silent authentication to clusters that are protected by Azure Active Directory.
##   OctopusAzureFabricCopyPackageTimeoutSec                            // Timeout in seconds for copying application package to image store.
##   
## --------------------------------------------------------------------------------------
##   Examples:
##
##   Deploy the application using the default package location for a Debug build.
##   . Scripts\Deploy-FabricApplication.ps1 -OctopusAzureFabricApplicationPackagePath 'pkg\Debug'
##   
##   Deploy the application but do not create the application instance.
##   . Scripts\Deploy-FabricApplication.ps1 -OctopusAzureFabricApplicationPackagePath 'pkg\Debug' -DoNotCreateApplication
##   
##   Deploy the application by providing values for parameters that are defined in the application manifest.
##   . Scripts\Deploy-FabricApplication.ps1 -OctopusAzureFabricApplicationPackagePath 'pkg\Debug' -OctopusAzureFabricApplicationParameters @{CustomParameter1='MyValue'; CustomParameter2='MyValue'}
## --------------------------------------------------------------------------------------
##

Param
(
    [String]
    $OctopusAzureFabricPublishProfileFile,

    [String]
    $OctopusAzureFabricApplicationPackagePath,

    [Switch]
    $OctopusAzureFabricDeployOnly,

    [Hashtable]
    $OctopusAzureFabricApplicationParameters,

    [Boolean]
    $OctopusAzureFabricUnregisterUnusedApplicationVersionsAfterUpgrade,

    [String]
    [ValidateSet('None', 'ForceUpgrade', 'VetoUpgrade')]
    $OctopusAzureFabricOverrideUpgradeBehavior = 'None',

    [Switch]
    $OctopusAzureFabricUseExistingClusterConnection,

    [String]
    [ValidateSet('Never','Always','SameAppTypeAndVersion')]
    $OctopusAzureFabricOverwriteBehavior = 'Never',

    [Switch]
    $OctopusAzureFabricSkipPackageValidation,

    [String]
    $OctopusAzureFabricSecurityToken,

    [int]
    $OctopusAzureFabricCopyPackageTimeoutSec
)

Write-Host "TODO: markse - remove this logging"
Write-Host "OctopusAzureFabricPublishProfileFile = $($OctopusAzureFabricPublishProfileFile)"
Write-Host "OctopusAzureFabricApplicationPackagePath = $($OctopusAzureFabricApplicationPackagePath)"
Write-Host "OctopusAzureFabricDeployOnly = $($OctopusAzureFabricDeployOnly)"
Write-Host "OctopusAzureFabricApplicationParameters = $($OctopusAzureFabricApplicationParameters)"
Write-Host "OctopusAzureFabricUnregisterUnusedApplicationVersionsAfterUpgrade = $($OctopusAzureFabricUnregisterUnusedApplicationVersionsAfterUpgrade)"
Write-Host "OctopusAzureFabricOverrideUpgradeBehavior = $($OctopusAzureFabricOverrideUpgradeBehavior)"
Write-Host "OctopusAzureFabricUseExistingClusterConnection = $($OctopusAzureFabricUseExistingClusterConnection)"
Write-Host "OctopusAzureFabricOverwriteBehavior = $($OctopusAzureFabricOverwriteBehavior)"
Write-Host "OctopusAzureFabricSkipPackageValidation = $($OctopusAzureFabricSkipPackageValidation)"
Write-Host "OctopusAzureFabricSecurityToken = $($OctopusAzureFabricSecurityToken)"
Write-Host "OctopusAzureFabricCopyPackageTimeoutSec = $($OctopusAzureFabricCopyPackageTimeoutSec)"

function Read-XmlElementAsHashtable
{
    Param (
        [System.Xml.XmlElement]
        $Element
    )

    $hashtable = @{}
    if ($Element.Attributes)
    {
        $Element.Attributes | 
            ForEach-Object {
                $boolVal = $null
                if ([bool]::TryParse($_.Value, [ref]$boolVal)) {
                    $hashtable[$_.Name] = $boolVal
                }
                else {
                    $hashtable[$_.Name] = $_.Value
                }
            }
    }

    return $hashtable
}

function Read-PublishProfile
{
    Param (
        [ValidateScript({Test-Path $_ -PathType Leaf})]
        [String]
        $OctopusAzureFabricPublishProfileFile
    )

    $publishProfileXml = [Xml] (Get-Content $OctopusAzureFabricPublishProfileFile)
    $publishProfile = @{}

    $publishProfile.ClusterConnectionParameters = Read-XmlElementAsHashtable $publishProfileXml.PublishProfile.Item("ClusterConnectionParameters")
    $publishProfile.UpgradeDeployment = Read-XmlElementAsHashtable $publishProfileXml.PublishProfile.Item("UpgradeDeployment")

    if ($publishProfileXml.PublishProfile.Item("UpgradeDeployment"))
    {
        $publishProfile.UpgradeDeployment.Parameters = Read-XmlElementAsHashtable $publishProfileXml.PublishProfile.Item("UpgradeDeployment").Item("Parameters")
        if ($publishProfile.UpgradeDeployment["Mode"])
        {
            $publishProfile.UpgradeDeployment.Parameters[$publishProfile.UpgradeDeployment["Mode"]] = $true
        }
    }

    $publishProfileFolder = (Split-Path $OctopusAzureFabricPublishProfileFile)
    $publishProfile.OctopusAzureFabricApplicationParametersFile = [System.IO.Path]::Combine($PublishProfileFolder, $publishProfileXml.PublishProfile.OctopusAzureFabricApplicationParametersFile.Path)

    return $publishProfile
}

# TODO: markse - removed default fallbacks.
#$LocalFolder = (Split-Path $MyInvocation.MyCommand.Path)

#if (!$OctopusAzureFabricPublishProfileFile)
#{
#    $OctopusAzureFabricPublishProfileFile = "$LocalFolder\..\PublishProfiles\Local.xml"
#}

#if (!$OctopusAzureFabricApplicationPackagePath)
#{
#    $OctopusAzureFabricApplicationPackagePath = "$LocalFolder\..\pkg\Release"
#}

$OctopusAzureFabricApplicationPackagePath = Resolve-Path $OctopusAzureFabricApplicationPackagePath

$publishProfile = Read-PublishProfile $OctopusAzureFabricPublishProfileFile

if (-not $OctopusAzureFabricUseExistingClusterConnection)
{
    $ClusterConnectionParameters = $publishProfile.ClusterConnectionParameters
    if ($OctopusAzureFabricSecurityToken)
    {
        $ClusterConnectionParameters["OctopusAzureFabricSecurityToken"] = $OctopusAzureFabricSecurityToken
    }

    try
    {
        [void](Connect-ServiceFabricCluster @ClusterConnectionParameters)
    }
    catch [System.Fabric.FabricObjectClosedException]
    {
        Write-Warning "Service Fabric cluster may not be connected."
        throw
    }
}

# TODO: markse - removed registry lookups in favour of local SDK folder.
#$RegKey = "HKLM:\SOFTWARE\Microsoft\Service Fabric SDK"
#$ModuleFolderPath = (Get-ItemProperty -Path $RegKey -Name FabricSDKPSModulePath).FabricSDKPSModulePath
$ModuleFolderPath = ".\ServiceFabricSDK"
Import-Module "$ModuleFolderPath\ServiceFabricSDK.psm1"

$IsUpgrade = ($publishProfile.UpgradeDeployment -and $publishProfile.UpgradeDeployment.Enabled -and $OctopusAzureFabricOverrideUpgradeBehavior -ne 'VetoUpgrade') -or $OctopusAzureFabricOverrideUpgradeBehavior -eq 'ForceUpgrade'
 
# check if this application exists or not
$ManifestFilePath = "$OctopusAzureFabricApplicationPackagePath\ApplicationManifest.xml"
$manifestXml = [Xml] (Get-Content $ManifestFilePath)
$AppTypeName = $manifestXml.ApplicationManifest.ApplicationTypeName
$AppExists = (Get-ServiceFabricApplication | ? { $_.ApplicationTypeName -eq $AppTypeName }) -ne $null
 
if ($IsUpgrade -and $AppExists)
{
    $Action = "RegisterAndUpgrade"
    if ($OctopusAzureFabricDeployOnly)
    {
        $Action = "Register"
    }
    
    $UpgradeParameters = $publishProfile.UpgradeDeployment.Parameters

    if ($OctopusAzureFabricOverrideUpgradeBehavior -eq 'ForceUpgrade')
    {
        # Warning: Do not alter these upgrade parameters. It will create an inconsistency with Visual Studio's behavior.
        $UpgradeParameters = @{ UnmonitoredAuto = $true; Force = $true }
    }

    if ($OctopusAzureFabricCopyPackageTimeoutSec)
    {
        Publish-UpgradedServiceFabricApplication -OctopusAzureFabricApplicationPackagePath $OctopusAzureFabricApplicationPackagePath -OctopusAzureFabricApplicationParametersFilePath $publishProfile.OctopusAzureFabricApplicationParametersFile -Action $Action -UpgradeParameters $UpgradeParameters -OctopusAzureFabricApplicationParameters $OctopusAzureFabricApplicationParameters -UnregisterUnusedVersions:$OctopusAzureFabricUnregisterUnusedApplicationVersionsAfterUpgrade -OctopusAzureFabricCopyPackageTimeoutSec $OctopusAzureFabricCopyPackageTimeoutSec -ErrorAction Stop
    }
    else
    {
        Publish-UpgradedServiceFabricApplication -OctopusAzureFabricApplicationPackagePath $OctopusAzureFabricApplicationPackagePath -OctopusAzureFabricApplicationParametersFilePath $publishProfile.OctopusAzureFabricApplicationParametersFile -Action $Action -UpgradeParameters $UpgradeParameters -OctopusAzureFabricApplicationParameters $OctopusAzureFabricApplicationParameters -UnregisterUnusedVersions:$OctopusAzureFabricUnregisterUnusedApplicationVersionsAfterUpgrade -ErrorAction Stop
    }
}
else
{
    $Action = "RegisterAndCreate"
    if ($OctopusAzureFabricDeployOnly)
    {
        $Action = "Register"
    }
    
    if ($OctopusAzureFabricCopyPackageTimeoutSec)
    {
        Publish-NewServiceFabricApplication -OctopusAzureFabricApplicationPackagePath $OctopusAzureFabricApplicationPackagePath -OctopusAzureFabricApplicationParametersFilePath $publishProfile.OctopusAzureFabricApplicationParametersFile -Action $Action -OctopusAzureFabricApplicationParameters $OctopusAzureFabricApplicationParameters -OctopusAzureFabricOverwriteBehavior $OctopusAzureFabricOverwriteBehavior -OctopusAzureFabricSkipPackageValidation:$OctopusAzureFabricSkipPackageValidation -OctopusAzureFabricCopyPackageTimeoutSec $OctopusAzureFabricCopyPackageTimeoutSec -ErrorAction Stop
    }
    else
    {
        Publish-NewServiceFabricApplication -OctopusAzureFabricApplicationPackagePath $OctopusAzureFabricApplicationPackagePath -OctopusAzureFabricApplicationParametersFilePath $publishProfile.OctopusAzureFabricApplicationParametersFile -Action $Action -OctopusAzureFabricApplicationParameters $OctopusAzureFabricApplicationParameters -OctopusAzureFabricOverwriteBehavior $OctopusAzureFabricOverwriteBehavior -OctopusAzureFabricSkipPackageValidation:$OctopusAzureFabricSkipPackageValidation -ErrorAction Stop
    }
}