## Octopus Azure Service Fabric Application script, version 1.0
## --------------------------------------------------------------------------------------
##
## This script is used to control how we deploy packages to Azure Service Fabric applications to a cluster. 
##
## This is a modified version of the Visual Studio's Service Fabric 'Deploy-FabricApplication.ps1' file. This
## version includes automatic support for 'fresh install vs upgrade' scenarios. Thx to Colin Dembovsky at
## http://colinsalmcorner.com/post/continuous-deployment-of-service-fabric-apps-using-vsts-or-tfs for posting 
## about this.
##
## If you want to customize the Azure deployment process, simply copy this script into
## your deployment package as DeployToServiceFabric.ps1. Octopus will invoke it instead of the default 
## script. 
##
## The script will be passed the following parameters in addition to the normal Octopus 
## variables passed to any PowerShell script.
##
##   PublishProfileFile                               // Path to the file containing the publish profile.
##   ApplicationPackagePath                           // Path to the folder of the packaged Service Fabric application.
##   DeployOnly                                       // Indicates that the Service Fabric application should not be created or upgraded after registering the application type.
##   ApplicationParameter                             // Hashtable of the Service Fabric application parameters to be used for the application.
##   UnregisterUnusedApplicationVersionsAfterUpgrade  // Indicates whether to unregister any unused application versions that exist after an upgrade is finished.
##   OverrideUpgradeBehavior                          // Indicates the behavior used to override the upgrade settings specified by the publish profile. Options: None | ForceUpgrade | VetoUpgrade
##   UseExistingClusterConnection                     // Indicates that the script should make use of an existing cluster connection that has already been established in the PowerShell session.  The cluster connection parameters configured in the publish profile are ignored.
##   OverwriteBehavior                                // Overwrite Behavior if an application exists in the cluster with the same name. This setting is not applicable when upgrading an application. Options: Never | Always | SameAppTypeAndVersion
##   SkipPackageValidation                            // Switch signaling whether the package should be validated or not before deployment.
##   SecurityToken                                    // A security token for authentication to cluster management endpoints. Used for silent authentication to clusters that are protected by Azure Active Directory.
##   CopyPackageTimeoutSec                            // Timeout in seconds for copying application package to image store.
##   
## --------------------------------------------------------------------------------------
##   Examples:
##
##   Deploy the application using the default package location for a Debug build.
##   . Scripts\Deploy-FabricApplication.ps1 -ApplicationPackagePath 'pkg\Debug'
##   
##   Deploy the application but do not create the application instance.
##   . Scripts\Deploy-FabricApplication.ps1 -ApplicationPackagePath 'pkg\Debug' -DoNotCreateApplication
##   
##   Deploy the application by providing values for parameters that are defined in the application manifest.
##   . Scripts\Deploy-FabricApplication.ps1 -ApplicationPackagePath 'pkg\Debug' -ApplicationParameter @{CustomParameter1='MyValue'; CustomParameter2='MyValue'}
## --------------------------------------------------------------------------------------
##

# Parse our Octopus string output variables into valid types (for the calls to Azure PowerShell cmdlets).
$DeployOnly = [System.Convert]::ToBoolean($DeployOnly)
$UnregisterUnusedApplicationVersionsAfterUpgrade = [System.Convert]::ToBoolean($UnregisterUnusedApplicationVersionsAfterUpgrade)
$UseExistingClusterConnection = [System.Convert]::ToBoolean($UseExistingClusterConnection)
$SkipPackageValidation = [System.Convert]::ToBoolean($SkipPackageValidation)
$CopyPackageTimeoutSec = [System.Convert]::ToInt32($CopyPackageTimeoutSec)

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
        $PublishProfileFile
    )

    $publishProfileXml = [Xml] (Get-Content $PublishProfileFile)
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

    $publishProfileFolder = (Split-Path $PublishProfileFile)
    $publishProfile.ApplicationParameterFile = [System.IO.Path]::Combine($PublishProfileFolder, $publishProfileXml.PublishProfile.ApplicationParameterFile.Path)

    return $publishProfile
}

$ApplicationPackagePath = Resolve-Path $ApplicationPackagePath

$publishProfile = Read-PublishProfile $PublishProfileFile

# This global clusterConnection should be set by now, from our ServiceFabricContext.
if (-not $global:clusterConnection)
{
    Write-Warning "Service Fabric cluster may not be connected."
    throw
}

$RegKey = "HKLM:\SOFTWARE\Microsoft\Service Fabric SDK"
$ModuleFolderPath = (Get-ItemProperty -Path $RegKey -Name FabricSDKPSModulePath).FabricSDKPSModulePath
Write-Verbose "Importing ServiceFabricSDK modules from $($ModuleFolderPath)"
Import-Module "$ModuleFolderPath\ServiceFabricSDK.psm1"

$IsUpgrade = ($publishProfile.UpgradeDeployment -and $publishProfile.UpgradeDeployment.Enabled -and $OverrideUpgradeBehavior -ne 'VetoUpgrade') -or $OverrideUpgradeBehavior -eq 'ForceUpgrade'

# check if this application exists or not
$ManifestFilePath = "$ApplicationPackagePath\ApplicationManifest.xml"
$manifestXml = [Xml] (Get-Content $ManifestFilePath)
$AppTypeName = $manifestXml.ApplicationManifest.ApplicationTypeName
$AppExists = (Get-ServiceFabricApplication | ? { $_.ApplicationTypeName -eq $AppTypeName }) -ne $null
 
if ($IsUpgrade -and $AppExists)
{
    $Action = "RegisterAndUpgrade"
    if ($DeployOnly)
    {
        $Action = "Register"
    }

    $UpgradeParameters = $publishProfile.UpgradeDeployment.Parameters

    if ($OverrideUpgradeBehavior -eq 'ForceUpgrade')
    {
        # Warning: Do not alter these upgrade parameters. It will create an inconsistency with Visual Studio's behavior.
        $UpgradeParameters = @{ UnmonitoredAuto = $true; Force = $true }
    }

    if ($CopyPackageTimeoutSec)
    {
        Publish-UpgradedServiceFabricApplication -ApplicationPackagePath $ApplicationPackagePath -ApplicationParameterFilePath $publishProfile.ApplicationParameterFile -Action $Action -UpgradeParameters $UpgradeParameters -ApplicationParameter $ApplicationParameter -UnregisterUnusedVersions:$UnregisterUnusedApplicationVersionsAfterUpgrade -CopyPackageTimeoutSec $CopyPackageTimeoutSec -ErrorAction Stop
    }
    else
    {
        Publish-UpgradedServiceFabricApplication -ApplicationPackagePath $ApplicationPackagePath -ApplicationParameterFilePath $publishProfile.ApplicationParameterFile -Action $Action -UpgradeParameters $UpgradeParameters -ApplicationParameter $ApplicationParameter -UnregisterUnusedVersions:$UnregisterUnusedApplicationVersionsAfterUpgrade -ErrorAction Stop
    }
}
else
{
    $Action = "RegisterAndCreate"
    if ($DeployOnly)
    {
        $Action = "Register"
    }
    
    if ($CopyPackageTimeoutSec)
    {
        Publish-NewServiceFabricApplication -ApplicationPackagePath $ApplicationPackagePath -ApplicationParameterFilePath $publishProfile.ApplicationParameterFile -Action $Action -ApplicationParameter $ApplicationParameter -OverwriteBehavior $OverwriteBehavior -SkipPackageValidation:$SkipPackageValidation -CopyPackageTimeoutSec $CopyPackageTimeoutSec -ErrorAction Stop
    }
    else
    {
        Publish-NewServiceFabricApplication -ApplicationPackagePath $ApplicationPackagePath -ApplicationParameterFilePath $publishProfile.ApplicationParameterFile -Action $Action -ApplicationParameter $ApplicationParameter -OverwriteBehavior $OverwriteBehavior -SkipPackageValidation:$SkipPackageValidation -ErrorAction Stop
    }
}
