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
##   RegisterApplicationTypeTimeoutSec                // Timeout in seconds for registering application type
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
$SkipPackageValidation = [System.Convert]::ToBoolean($SkipPackageValidation)
$CopyPackageTimeoutSec = [System.Convert]::ToInt32($CopyPackageTimeoutSec)
$RegisterApplicationTypeTimeoutSec = [System.Convert]::ToInt32($RegisterApplicationTypeTimeoutSec)

function Read-XmlElementAsHashtable {
    Param (
        [System.Xml.XmlElement]
        $Element
    )

    $hashtable = @{}
    if ($Element.Attributes) {
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

function Read-PublishProfile {
    Param (
        [ValidateScript( {Test-Path $_ -PathType Leaf})]
        [String]
        $PublishProfileFile
    )

    $publishProfileXml = [Xml] (Get-Content $PublishProfileFile)
    $publishProfile = @{}

    $publishProfile.ClusterConnectionParameters = Read-XmlElementAsHashtable $publishProfileXml.PublishProfile.Item("ClusterConnectionParameters")
    $publishProfile.UpgradeDeployment = Read-XmlElementAsHashtable $publishProfileXml.PublishProfile.Item("UpgradeDeployment")

    if ($publishProfileXml.PublishProfile.Item("UpgradeDeployment")) {
        $publishProfile.UpgradeDeployment.Parameters = Read-XmlElementAsHashtable $publishProfileXml.PublishProfile.Item("UpgradeDeployment").Item("Parameters")
        if ($publishProfile.UpgradeDeployment["Mode"]) {
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
if (-not $global:clusterConnection) {
    Write-Warning "Service Fabric cluster may not be connected."
    throw
}

$RegKey = "HKLM:\SOFTWARE\Microsoft\Service Fabric SDK"
$ModuleFolderPath = (Get-ItemProperty -Path $RegKey -Name FabricSDKPSModulePath).FabricSDKPSModulePath
Write-Verbose "Importing ServiceFabricSDK modules from $($ModuleFolderPath)"
Import-Module "$ModuleFolderPath\ServiceFabricSDK.psm1"

# check if this application exists or not
$ManifestFilePath = "$ApplicationPackagePath\ApplicationManifest.xml"
$manifestXml = [Xml] (Get-Content $ManifestFilePath)
$AppTypeName = $manifestXml.ApplicationManifest.ApplicationTypeName
$AppTypeVersion = $manifestXml.ApplicationManifest.ApplicationTypeVersion
$AppName = Get-ApplicationNameFromApplicationParameterFile $publishProfile.ApplicationParameterFile
$AppTypeAndNameExists = $null -ne (Get-ServiceFabricApplication | ? { $_.ApplicationTypeName -eq $AppTypeName -and $_.ApplicationName -eq $AppName })
$AppTypeAndNameAndVersionExists = $null -ne (Get-ServiceFabricApplication | ? { $_.ApplicationTypeName -eq $AppTypeName -and $_.ApplicationName -eq $AppName -and $_.ApplicationTypeVersion -eq $AppTypeVersion})
$AppTypeAndVersionExists = $null -ne (Get-ServiceFabricApplicationType -ApplicationTypeName $AppTypeName | Where-Object { $_.ApplicationTypeVersion -eq $AppTypeVersion })
$UpgradeEnabledInProfile = $publishProfile.UpgradeDeployment -and $publishProfile.UpgradeDeployment.Enabled
$UpgradeEnabled = $UpgradeEnabledInProfile -and -not ($OverrideUpgradeBehavior -eq 'VetoUpgrade')
$ForceUpgrade = $OverrideUpgradeBehavior -eq 'ForceUpgrade'
$requiresRegister = $false

Write-Verbose "App Type Name And Version Exists: $AppTypeAndVersionExists"
Write-Verbose "Application Type and Name Exists: $AppTypeAndNameExists"
Write-Verbose "Application Type and Name and Version Exists: $AppTypeAndNameAndVersionExists"
Write-Verbose "Application Type Name: $AppTypeName"
Write-Verbose "Application Type Version: $AppTypeVersion"
Write-Verbose "Application Name: $AppName"
Write-Verbose "Force Upgrade based on Override upgrade behavior '$OverrideUpgradeBehavior': $ForceUpgrade"
Write-Verbose "Upgrade enabled in profile: $UpgradeEnabledInProfile"
Write-Verbose "Found Application: '$AppTypeName' with version '$AppTypeVersion'"

$parameters = @{
    ApplicationPackagePath       = $ApplicationPackagePath
    ApplicationParameterFilePath = $publishProfile.ApplicationParameterFile
    ApplicationParameter         = $ApplicationParameter
    ErrorAction                  = 'Stop'
}

if (-not $AppTypeAndVersionExists) {
    if ($CopyPackageTimeoutSec) {
        $parameters.CopyPackageTimeoutSec = $CopyPackageTimeoutSec
    }

    if ($RegisterApplicationTypeTimeoutSec) {
        Get-Help Publish-NewServiceFabricApplication -Parameter RegisterApplicationTypeTimeoutSec -ErrorVariable timeoutParamMissing -ErrorAction SilentlyContinue | Out-Null
        if (!$timeoutParamMissing) {
            $parameters.RegisterApplicationTypeTimeoutSec = $RegisterApplicationTypeTimeoutSec
        }
        else {
            Write-Warning "A value was supplied for RegisterApplicationTypeTimeoutSec but the current Service Fabric SDK doesn't support it."
        }
    }

    $requiresRegister = $true
}

# Perform an upgrade if upgrades are enabled, the app type and name exists, but the new version does not exist,
# or if an upgrade was forced and the app type and name exists
if ($AppTypeAndNameExists -and ($UpgradeEnabled -and -not $AppTypeAndNameAndVersionExists -or $ForceUpgrade)) {
    if ($DeployOnly) {
        $parameters.Action = "Register"
    }
    elseif ($requiresRegister) {
        $parameters.Action = "RegisterAndUpgrade"
    }
    else {
        $parameters.Action = "Upgrade"
    }

    $UpgradeParameters = $publishProfile.UpgradeDeployment.Parameters

    if ($null -eq $UpgradeParameters) {
        $UpgradeParameters = @{ UnmonitoredAuto = $true }
    }

    if ($ForceUpgrade) {
        # Warning: Do not alter these upgrade parameters. It will create an inconsistency with Visual Studio's behavior.
        $UpgradeParameters = @{ UnmonitoredAuto = $true; Force = $true }
    }

    $parameters.UnregisterUnusedVersions = $UnregisterUnusedApplicationVersionsAfterUpgrade
    $parameters.UpgradeParameters = $UpgradeParameters

    Write-Host "Performing '$($parameters.Action)' action"
    Write-Verbose "Parameters: "
    Write-Verbose $($parameters | Out-String)
    Publish-UpgradedServiceFabricApplication @parameters
}
# Otherwise we are registering or creating the app
else {
    if ($DeployOnly) {
        $parameters.Action = "Register"
    }
    elseif ($requiresRegister -or $AppTypeAndNameAndVersionExists) {
        $parameters.Action = "RegisterAndCreate"
    }
    else {
        $parameters.Action = "Create"
    }

    $parameters.OverwriteBehavior = $OverwriteBehavior
    $parameters.SkipPackageValidation = $SkipPackageValidation

    Write-Host "Performing '$($parameters.Action)' action"
    Write-Verbose "Parameters: "
    Write-Verbose $($parameters | Out-String)
    Publish-NewServiceFabricApplication @parameters
}
