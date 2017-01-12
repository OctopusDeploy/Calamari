function Publish-UpgradedServiceFabricApplication
{
    <#
    .SYNOPSIS 
    Publishes and starts an upgrade for an existing Service Fabric application in Service Fabric cluster.

    .DESCRIPTION
    This script registers & starts an upgrade for Service Fabric application.

    .NOTES
    Connection to service fabric cluster should be established by using 'Connect-ServiceFabricCluster' before invoking this cmdlet.

    .PARAMETER ApplicationPackagePath
    Path to the folder containing the Service Fabric application package OR path to the zipped service fabric applciation package.

    .PARAMETER ApplicationParameterFilePath
    Path to the application parameter file which contains Application Name and application parameters to be used for the application.    

    .PARAMETER ApplicationName
    Name of Service Fabric application to be created. If value for this parameter is provided alongwith ApplicationParameterFilePath it will override the Application name specified in ApplicationParameter file.

    .PARAMETER Action
    Action which this script performs. Available Options are Register, Upgrade, RegisterAndUpgrade. Default Action is RegisterAndUpgrade.

    .PARAMETER ApplicationParameter
    Hashtable of the Service Fabric application parameters to be used for the application. If value for this parameter is provided, it will be merged with application parameters
    specified in ApplicationParameter file. In case a parameter is found ina pplication parameter file and on commandline, commandline parameter will override the one specified in application parameter file.

    .PARAMETER UpgradeParameters
    Hashtable of the upgrade parameters to be used for this upgrade. If Upgrade parameters are not specified then script will perform an UnmonitoredAuto upgrade.

    .PARAMETER UnregisterUnusedVersions
    Switch signalling if older vesions of the application need to be unregistered after upgrade.

    .PARAMETER SkipPackageValidation
    Switch signaling whether the package should be validated or not before deployment.

    .PARAMETER CopyPackageTimeoutSec
    Timeout in seconds for copying application package to image store. Default is 600 seconds.

    .EXAMPLE
    Publish-UpgradeServiceFabricApplication -ApplicationPackagePath 'pkg\Debug' -ApplicationParameterFilePath 'AppParameters.Local.xml'

    Registers & Upgrades an application with AppParameter file containing name of application and values for parameters that are defined in the application manifest.

    Publish-UpgradesServiceFabricApplication -ApplicationPackagePath 'pkg\Debug' -ApplicationName 'fabric:/Application1'

    Registers & Upgrades an application with the specified applciation name.

    #>

    [CmdletBinding(DefaultParameterSetName="ApplicationName")]  
    Param
    (
        [Parameter(Mandatory=$true,ParameterSetName="ApplicationParameterFilePath")]
        [Parameter(Mandatory=$true,ParameterSetName="ApplicationName")]
        [String]$ApplicationPackagePath,

        [Parameter(Mandatory=$true,ParameterSetName="ApplicationParameterFilePath")]
        [String]$ApplicationParameterFilePath,

        [Parameter(Mandatory=$true,ParameterSetName="ApplicationName")]
        [String]$ApplicationName,

        [Parameter(ParameterSetName="ApplicationParameterFilePath")]
        [Parameter(ParameterSetName="ApplicationName")]
        [ValidateSet('Register','Upgrade','RegisterAndUpgrade')]
        [String]$Action = 'RegisterAndUpgrade',

        [Parameter(ParameterSetName="ApplicationParameterFilePath")]
        [Parameter(ParameterSetName="ApplicationName")]
        [Hashtable]$ApplicationParameter,

        [Parameter(ParameterSetName="ApplicationParameterFilePath")]
        [Parameter(ParameterSetName="ApplicationName")]
        [Hashtable]$UpgradeParameters = @{UnmonitoredAuto = $true},

        [Parameter(ParameterSetName="ApplicationParameterFilePath")]
        [Parameter(ParameterSetName="ApplicationName")]
        [Switch]$UnregisterUnusedVersions,

        [Parameter(ParameterSetName="ApplicationParameterFilePath")]
        [Parameter(ParameterSetName="ApplicationName")]
        [Switch]$SkipPackageValidation,

        [Parameter(ParameterSetName="ApplicationParameterFilePath")]
        [Parameter(ParameterSetName="ApplicationName")]
        [int]$CopyPackageTimeoutSec = 600
    )


    if (!(Test-Path $ApplicationPackagePath))
    {
        $errMsg = "$ApplicationPackagePath is not found."
        throw $errMsg
    }

    if (Test-Path $ApplicationPackagePath -PathType Leaf)
    {
        if((Get-Item $ApplicationPackagePath).Extension -eq ".sfpkg")
        {
            $AppPkgPathToUse=[io.path]::combine($env:Temp, (Get-Item $ApplicationPackagePath).BaseName)
            Expand-ToFolder $ApplicationPackagePath $AppPkgPathToUse
        }
        else
        {
            $errMsg = "$ApplicationPackagePath is not a valid Service Fabric application package"
            throw $errMsg
        }
    }
    else
    {
        $AppPkgPathToUse = $ApplicationPackagePath
    }

    if ($PSBoundParameters.ContainsKey('ApplicationParameterFilePath') -and !(Test-Path $ApplicationParameterFilePath -PathType Leaf))
    {
        $errMsg = "$ApplicationParameterFilePath is not found."
        throw $errMsg
    }

	# Get image store connection string
    $clusterManifestText = Get-ServiceFabricClusterManifest
	$imageStoreConnectionString = Get-ImageStoreConnectionStringFromClusterManifest ([xml] $clusterManifestText)

    if(!$SkipPackageValidation)
    {
        $packageValidationSuccess = (Test-ServiceFabricApplicationPackage $AppPkgPathToUse -ImageStoreConnectionString $imageStoreConnectionString)
        if (!$packageValidationSuccess)
        {
           $errMsg = "Validation failed for package: " +  $ApplicationPackagePath
           throw $errMsg
        }
    }

    $ApplicationManifestPath = "$AppPkgPathToUse\ApplicationManifest.xml"    

    try
    {
        [void](Test-ServiceFabricClusterConnection)
    }
    catch
    {
        Write-Warning "Unable to verify connection to Service Fabric cluster."
        throw
    }

    # If ApplicationName is not specified on command line get application name from Application parameter file.
    if(!$ApplicationName)
    {
       $ApplicationName = Get-ApplicationNameFromApplicationParameterFile $ApplicationParameterFilePath
    }

    $names = Get-NamesFromApplicationManifest -ApplicationManifestPath $ApplicationManifestPath
    if (!$names)
    {
        return
    }

    if ($Action.Equals('RegisterAndUpgrade') -or $Action.Equals('Register'))
    {    
        ## Check existence of the application
        $oldApplication = Get-ServiceFabricApplication -ApplicationName $ApplicationName
        
        if (!$oldApplication)
        {
            $errMsg = "Application '$ApplicationName' doesn't exist in cluster."
            throw $errMsg
        }
        else
        {
            if($oldApplication.ApplicationTypeName -ne $names.ApplicationTypeName)
            {   
                $errMsg =  "Application type of application '$ApplicationName' doesn't match with the Application Type in application manifest specified in the new application package.
                Please ensure that the application being upgraded is of the same Applciation Type."
                throw $errMsg
            }
        }                
    
        ## Check upgrade status
        $upgradeStatus = Get-ServiceFabricApplicationUpgrade -ApplicationName $ApplicationName
        if ($upgradeStatus.UpgradeState -ne "RollingBackCompleted" -and $upgradeStatus.UpgradeState -ne "RollingForwardCompleted")
        {
            $errMsg = "An upgrade for the application '$names.ApplicationTypeName' is already in progress."
            throw $errMsg
        }

        $reg = Get-ServiceFabricApplicationType -ApplicationTypeName $names.ApplicationTypeName | Where-Object  { $_.ApplicationTypeVersion -eq $names.ApplicationTypeVersion }
        if ($reg)
        {
            Write-Host 'Application Type '$names.ApplicationTypeName' and Version '$names.ApplicationTypeVersion' was already registered with Cluster, unregistering it...'
            $reg | Unregister-ServiceFabricApplicationType -Force
        }
    
        $applicationPackagePathInImageStore = $names.ApplicationTypeName
        Write-Host "Copying application package to image store..."
        Copy-ServiceFabricApplicationPackage -ApplicationPackagePath $AppPkgPathToUse -ImageStoreConnectionString $imageStoreConnectionString -ApplicationPackagePathInImageStore $applicationPackagePathInImageStore -TimeOutSec $CopyPackageTimeoutSec
        if(!$?)
        {
            throw "Copying of application package to image store failed. Cannot continue with registering the application."
        }
    
        Write-Host "Registering application type..."
        Register-ServiceFabricApplicationType -ApplicationPathInImageStore $applicationPackagePathInImageStore
        if(!$?)
        {
            throw "Registration of application type failed."
        }
     }
    
    if ($Action.Equals('Upgrade') -or $Action.Equals('RegisterAndUpgrade'))
    {
        try
        {
            $UpgradeParameters["ApplicationName"] = $ApplicationName
            $UpgradeParameters["ApplicationTypeVersion"] = $names.ApplicationTypeVersion
        
             # If application parameters file is specified read values from and merge it with parameters passed on Commandline
            if ($PSBoundParameters.ContainsKey('ApplicationParameterFilePath'))
            {
                $appParamsFromFile = Get-ApplicationParametersFromApplicationParameterFile $ApplicationParameterFilePath        
                if(!$ApplicationParameter)
                {
                    $ApplicationParameter = $appParamsFromFile
                }
                else
                {
                    $ApplicationParameter = Merge-Hashtables -HashTableOld $appParamsFromFile -HashTableNew $ApplicationParameter
                }    
            }
     
            $UpgradeParameters["ApplicationParameter"] = $ApplicationParameter

            $serviceTypeHealthPolicyMap = $upgradeParameters["ServiceTypeHealthPolicyMap"]
            if ($serviceTypeHealthPolicyMap -and $serviceTypeHealthPolicyMap -is [string])
            {
                $upgradeParameters["ServiceTypeHealthPolicyMap"] = Invoke-Expression $serviceTypeHealthPolicyMap
            }
        
            Write-Host "Start upgrading application..." 
            Start-ServiceFabricApplicationUpgrade @UpgradeParameters
        }
        catch
        {
            Write-Host "Unregister application type '$names.ApplicationTypeName' and version '$names.ApplicationTypeVersion' ..."
            Unregister-ServiceFabricApplicationType -ApplicationTypeName $names.ApplicationTypeName -ApplicationTypeVersion $names.ApplicationTypeVersion -Force
            throw
        }

        if (!$UpgradeParameters["Monitored"] -and !$UpgradeParameters["UnmonitoredAuto"])
        {
            return
        }
    
        do
        {
            Write-Host "Waiting for upgrade..."
            Start-Sleep -Seconds 3
            $upgradeStatus = Get-ServiceFabricApplicationUpgrade -ApplicationName $ApplicationName
        } while ($upgradeStatus.UpgradeState -ne "RollingBackCompleted" -and $upgradeStatus.UpgradeState -ne "RollingForwardCompleted")
    
        if($UnregisterUnusedVersions)
        {
            Write-Host 'Unregistering other unused versions for the application type...'
            foreach($registeredAppTypes in Get-ServiceFabricApplicationType -ApplicationTypeName $names.ApplicationTypeName | Where-Object  { $_.ApplicationTypeVersion -ne $names.ApplicationTypeVersion })
            {
                try
                {
                    $registeredAppTypes | Unregister-ServiceFabricApplicationType -Force
                }
                catch [System.Fabric.FabricException]
                {
                    # AppType and Version in use.
                }
            }
        }

        if($upgradeStatus.UpgradeState -eq "RollingForwardCompleted")
        {
            Write-Host "Upgrade completed successfully."
        }
        elseif($upgradeStatus.UpgradeState -eq "RollingBackCompleted")
        {
            Write-Host "Upgrade was Rolled back."
        }
    }
}
# SIG # Begin signature block
# MIIa6QYJKoZIhvcNAQcCoIIa2jCCGtYCAQExCzAJBgUrDgMCGgUAMGkGCisGAQQB
# gjcCAQSgWzBZMDQGCisGAQQBgjcCAR4wJgIDAQAABBAfzDtgWUsITrck0sYpfvNR
# AgEAAgEAAgEAAgEAAgEAMCEwCQYFKw4DAhoFAAQUa8eLombc8BkFhitlommFZXKL
# zXWgghWDMIIEwzCCA6ugAwIBAgITMwAAAMZ4gDYBdRppcgAAAAAAxjANBgkqhkiG
# 9w0BAQUFADB3MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
# A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSEw
# HwYDVQQDExhNaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EwHhcNMTYwOTA3MTc1ODUz
# WhcNMTgwOTA3MTc1ODUzWjCBszELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hp
# bmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jw
# b3JhdGlvbjENMAsGA1UECxMETU9QUjEnMCUGA1UECxMebkNpcGhlciBEU0UgRVNO
# OkY1MjgtMzc3Ny04QTc2MSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBT
# ZXJ2aWNlMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEArQsjG6jKiCgU
# NuPDaF0GhCh1QYcSqJypNAJgoa1GtgoNrKXTDUZF6K+eHPNzXv9v/LaYLZX2GyOI
# 9lGz55tXVv1Ny6I1ueVhy2cUAhdE+IkVR6AtCo8Ar8uHwEpkyTi+4Ywr6sOGM7Yr
# wBqw+SeaBjBwON+8E8SAz0pgmHHj4cNvt5A6R+IQC6tyiFx+JEMO1qqnITSI2qx3
# kOXhD3yTF4YjjRnTx3HGpfawUCyfWsxasAHHlILEAfsVAmXsbr4XAC2HBZGKXo03
# jAmfvmbgbm3V4KBK296Unnp92RZmwAEqL08n+lrl+PEd6w4E9mtFHhR9wGSW29C5
# /0bOar9zHwIDAQABo4IBCTCCAQUwHQYDVR0OBBYEFNS/9jKwiDEP5hmU8T6/Mfpb
# Ag8JMB8GA1UdIwQYMBaAFCM0+NlSRnAK7UD7dvuzK7DDNbMPMFQGA1UdHwRNMEsw
# SaBHoEWGQ2h0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3Rz
# L01pY3Jvc29mdFRpbWVTdGFtcFBDQS5jcmwwWAYIKwYBBQUHAQEETDBKMEgGCCsG
# AQUFBzAChjxodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpL2NlcnRzL01pY3Jv
# c29mdFRpbWVTdGFtcFBDQS5jcnQwEwYDVR0lBAwwCgYIKwYBBQUHAwgwDQYJKoZI
# hvcNAQEFBQADggEBAJhbANzvo0iL5FA5Z5QkwG+PvkDfOaYsTYksqFk+MgpqzPxc
# FwSYME/S/wyihd4lwgQ6CPdO5AGz3m5DZU7gPS5FcCl10k9pTxZ4s857Pu8ZrE2x
# rnUyUiQFl5DYSNroRPuQYRZZXs2xK1WVn1JcwcAwJwfu1kwnebPD90o1DRlNozHF
# 3NMaIo0nCTRAN86eSByKdYpDndgpVLSoN2wUnsh4bLcZqod4ozdkvgGS7N1Af18R
# EFSUBVraf7MoSxKeNIKLLyhgNxDxZxrUgnPb3zL73zOj40A1Ibw3WzJob8vYK+gB
# YWORl4jm6vCwAq/591z834HDNH60Ud0bH+xS7PowggTtMIID1aADAgECAhMzAAAB
# QJap7nBW/swHAAEAAAFAMA0GCSqGSIb3DQEBBQUAMHkxCzAJBgNVBAYTAlVTMRMw
# EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVN
# aWNyb3NvZnQgQ29ycG9yYXRpb24xIzAhBgNVBAMTGk1pY3Jvc29mdCBDb2RlIFNp
# Z25pbmcgUENBMB4XDTE2MDgxODIwMTcxN1oXDTE3MTEwMjIwMTcxN1owgYMxCzAJ
# BgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25k
# MR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xDTALBgNVBAsTBE1PUFIx
# HjAcBgNVBAMTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjCCASIwDQYJKoZIhvcNAQEB
# BQADggEPADCCAQoCggEBANtLi+kDal/IG10KBTnk1Q6S0MThi+ikDQUZWMA81ynd
# ibdobkuffryavVSGOanxODUW5h2s+65r3Akw77ge32z4SppVl0jII4mzWSc0vZUx
# R5wPzkA1Mjf+6fNPpBqks3m8gJs/JJjE0W/Vf+dDjeTc8tLmrmbtBDohlKZX3APb
# LMYb/ys5qF2/Vf7dSd9UBZSrM9+kfTGmTb1WzxYxaD+Eaxxt8+7VMIruZRuetwgc
# KX6TvfJ9QnY4ItR7fPS4uXGew5T0goY1gqZ0vQIz+lSGhaMlvqqJXuI5XyZBmBre
# ueZGhXi7UTICR+zk+R+9BFF15hKbduuFlxQiCqET92ECAwEAAaOCAWEwggFdMBMG
# A1UdJQQMMAoGCCsGAQUFBwMDMB0GA1UdDgQWBBSc5ehtgleuNyTe6l6pxF+QHc7Z
# ezBSBgNVHREESzBJpEcwRTENMAsGA1UECxMETU9QUjE0MDIGA1UEBRMrMjI5ODAz
# K2Y3ODViMWMwLTVkOWYtNDMxNi04ZDZhLTc0YWU2NDJkZGUxYzAfBgNVHSMEGDAW
# gBTLEejK0rQWWAHJNy4zFha5TJoKHzBWBgNVHR8ETzBNMEugSaBHhkVodHRwOi8v
# Y3JsLm1pY3Jvc29mdC5jb20vcGtpL2NybC9wcm9kdWN0cy9NaWNDb2RTaWdQQ0Ff
# MDgtMzEtMjAxMC5jcmwwWgYIKwYBBQUHAQEETjBMMEoGCCsGAQUFBzAChj5odHRw
# Oi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpL2NlcnRzL01pY0NvZFNpZ1BDQV8wOC0z
# MS0yMDEwLmNydDANBgkqhkiG9w0BAQUFAAOCAQEAa+RW49cTHSBA+W3p3k7bXR7G
# bCaj9+UJgAz/V+G01Nn5XEjhBn/CpFS4lnr1jcmDEwxxv/j8uy7MFXPzAGtOJar0
# xApylFKfd00pkygIMRbZ3250q8ToThWxmQVEThpJSSysee6/hU+EbkfvvtjSi0lp
# DimD9aW9oxshraKlPpAgnPWfEj16WXVk79qjhYQyEgICamR3AaY5mLPuoihJbKwk
# Mig+qItmLPsC2IMvI5KR91dl/6TV6VEIlPbW/cDVwCBF/UNJT3nuZBl/YE7ixMpT
# Th/7WpENW80kg3xz6MlCdxJfMSbJsM5TimFU98KNcpnxxbYdfqqQhAQ6l3mtYDCC
# BbwwggOkoAMCAQICCmEzJhoAAAAAADEwDQYJKoZIhvcNAQEFBQAwXzETMBEGCgmS
# JomT8ixkARkWA2NvbTEZMBcGCgmSJomT8ixkARkWCW1pY3Jvc29mdDEtMCsGA1UE
# AxMkTWljcm9zb2Z0IFJvb3QgQ2VydGlmaWNhdGUgQXV0aG9yaXR5MB4XDTEwMDgz
# MTIyMTkzMloXDTIwMDgzMTIyMjkzMloweTELMAkGA1UEBhMCVVMxEzARBgNVBAgT
# Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
# dCBDb3Jwb3JhdGlvbjEjMCEGA1UEAxMaTWljcm9zb2Z0IENvZGUgU2lnbmluZyBQ
# Q0EwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCycllcGTBkvx2aYCAg
# Qpl2U2w+G9ZvzMvx6mv+lxYQ4N86dIMaty+gMuz/3sJCTiPVcgDbNVcKicquIEn0
# 8GisTUuNpb15S3GbRwfa/SXfnXWIz6pzRH/XgdvzvfI2pMlcRdyvrT3gKGiXGqel
# cnNW8ReU5P01lHKg1nZfHndFg4U4FtBzWwW6Z1KNpbJpL9oZC/6SdCnidi9U3RQw
# WfjSjWL9y8lfRjFQuScT5EAwz3IpECgixzdOPaAyPZDNoTgGhVxOVoIoKgUyt0vX
# T2Pn0i1i8UU956wIAPZGoZ7RW4wmU+h6qkryRs83PDietHdcpReejcsRj1Y8wawJ
# XwPTAgMBAAGjggFeMIIBWjAPBgNVHRMBAf8EBTADAQH/MB0GA1UdDgQWBBTLEejK
# 0rQWWAHJNy4zFha5TJoKHzALBgNVHQ8EBAMCAYYwEgYJKwYBBAGCNxUBBAUCAwEA
# ATAjBgkrBgEEAYI3FQIEFgQU/dExTtMmipXhmGA7qDFvpjy82C0wGQYJKwYBBAGC
# NxQCBAweCgBTAHUAYgBDAEEwHwYDVR0jBBgwFoAUDqyCYEBWJ5flJRP8KuEKU5VZ
# 5KQwUAYDVR0fBEkwRzBFoEOgQYY/aHR0cDovL2NybC5taWNyb3NvZnQuY29tL3Br
# aS9jcmwvcHJvZHVjdHMvbWljcm9zb2Z0cm9vdGNlcnQuY3JsMFQGCCsGAQUFBwEB
# BEgwRjBEBggrBgEFBQcwAoY4aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraS9j
# ZXJ0cy9NaWNyb3NvZnRSb290Q2VydC5jcnQwDQYJKoZIhvcNAQEFBQADggIBAFk5
# Pn8mRq/rb0CxMrVq6w4vbqhJ9+tfde1MOy3XQ60L/svpLTGjI8x8UJiAIV2sPS9M
# uqKoVpzjcLu4tPh5tUly9z7qQX/K4QwXaculnCAt+gtQxFbNLeNK0rxw56gNogOl
# VuC4iktX8pVCnPHz7+7jhh80PLhWmvBTI4UqpIIck+KUBx3y4k74jKHK6BOlkU7I
# G9KPcpUqcW2bGvgc8FPWZ8wi/1wdzaKMvSeyeWNWRKJRzfnpo1hW3ZsCRUQvX/Ta
# rtSCMm78pJUT5Otp56miLL7IKxAOZY6Z2/Wi+hImCWU4lPF6H0q70eFW6NB4lhhc
# yTUWX92THUmOLb6tNEQc7hAVGgBd3TVbIc6YxwnuhQ6MT20OE049fClInHLR82zK
# wexwo1eSV32UjaAbSANa98+jZwp0pTbtLS8XyOZyNxL0b7E8Z4L5UrKNMxZlHg6K
# 3RDeZPRvzkbU0xfpecQEtNP7LN8fip6sCvsTJ0Ct5PnhqX9GuwdgR2VgQE6wQuxO
# 7bN2edgKNAltHIAxH+IOVN3lofvlRxCtZJj/UBYufL8FIXrilUEnacOTj5XJjdib
# Ia4NXJzwoq6GaIMMai27dmsAHZat8hZ79haDJLmIz2qoRzEvmtzjcT3XAH5iR9HO
# iMm4GPoOco3Boz2vAkBq/2mbluIQqBC0N1AI1sM9MIIGBzCCA++gAwIBAgIKYRZo
# NAAAAAAAHDANBgkqhkiG9w0BAQUFADBfMRMwEQYKCZImiZPyLGQBGRYDY29tMRkw
# FwYKCZImiZPyLGQBGRYJbWljcm9zb2Z0MS0wKwYDVQQDEyRNaWNyb3NvZnQgUm9v
# dCBDZXJ0aWZpY2F0ZSBBdXRob3JpdHkwHhcNMDcwNDAzMTI1MzA5WhcNMjEwNDAz
# MTMwMzA5WjB3MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
# A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSEw
# HwYDVQQDExhNaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EwggEiMA0GCSqGSIb3DQEB
# AQUAA4IBDwAwggEKAoIBAQCfoWyx39tIkip8ay4Z4b3i48WZUSNQrc7dGE4kD+7R
# p9FMrXQwIBHrB9VUlRVJlBtCkq6YXDAm2gBr6Hu97IkHD/cOBJjwicwfyzMkh53y
# 9GccLPx754gd6udOo6HBI1PKjfpFzwnQXq/QsEIEovmmbJNn1yjcRlOwhtDlKEYu
# J6yGT1VSDOQDLPtqkJAwbofzWTCd+n7Wl7PoIZd++NIT8wi3U21StEWQn0gASkdm
# EScpZqiX5NMGgUqi+YSnEUcUCYKfhO1VeP4Bmh1QCIUAEDBG7bfeI0a7xC1Un68e
# eEExd8yb3zuDk6FhArUdDbH895uyAc4iS1T/+QXDwiALAgMBAAGjggGrMIIBpzAP
# BgNVHRMBAf8EBTADAQH/MB0GA1UdDgQWBBQjNPjZUkZwCu1A+3b7syuwwzWzDzAL
# BgNVHQ8EBAMCAYYwEAYJKwYBBAGCNxUBBAMCAQAwgZgGA1UdIwSBkDCBjYAUDqyC
# YEBWJ5flJRP8KuEKU5VZ5KShY6RhMF8xEzARBgoJkiaJk/IsZAEZFgNjb20xGTAX
# BgoJkiaJk/IsZAEZFgltaWNyb3NvZnQxLTArBgNVBAMTJE1pY3Jvc29mdCBSb290
# IENlcnRpZmljYXRlIEF1dGhvcml0eYIQea0WoUqgpa1Mc1j0BxMuZTBQBgNVHR8E
# STBHMEWgQ6BBhj9odHRwOi8vY3JsLm1pY3Jvc29mdC5jb20vcGtpL2NybC9wcm9k
# dWN0cy9taWNyb3NvZnRyb290Y2VydC5jcmwwVAYIKwYBBQUHAQEESDBGMEQGCCsG
# AQUFBzAChjhodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpL2NlcnRzL01pY3Jv
# c29mdFJvb3RDZXJ0LmNydDATBgNVHSUEDDAKBggrBgEFBQcDCDANBgkqhkiG9w0B
# AQUFAAOCAgEAEJeKw1wDRDbd6bStd9vOeVFNAbEudHFbbQwTq86+e4+4LtQSooxt
# YrhXAstOIBNQmd16QOJXu69YmhzhHQGGrLt48ovQ7DsB7uK+jwoFyI1I4vBTFd1P
# q5Lk541q1YDB5pTyBi+FA+mRKiQicPv2/OR4mS4N9wficLwYTp2OawpylbihOZxn
# LcVRDupiXD8WmIsgP+IHGjL5zDFKdjE9K3ILyOpwPf+FChPfwgphjvDXuBfrTot/
# xTUrXqO/67x9C0J71FNyIe4wyrt4ZVxbARcKFA7S2hSY9Ty5ZlizLS/n+YWGzFFW
# 6J1wlGysOUzU9nm/qhh6YinvopspNAZ3GmLJPR5tH4LwC8csu89Ds+X57H2146So
# dDW4TsVxIxImdgs8UoxxWkZDFLyzs7BNZ8ifQv+AeSGAnhUwZuhCEl4ayJ4iIdBD
# 6Svpu/RIzCzU2DKATCYqSCRfWupW76bemZ3KOm+9gSd0BhHudiG/m4LBJ1S2sWo9
# iaF2YbRuoROmv6pH8BJv/YoybLL+31HIjCPJZr2dHYcSZAI9La9Zj7jkIeW1sMpj
# tHhUBdRBLlCslLCleKuzoJZ1GtmShxN1Ii8yqAhuoFuMJb+g74TKIdbrHk/Jmu5J
# 4PcBZW+JC33Iacjmbuqnl84xKf8OxVtc2E0bodj6L54/LlUWa8kTo/0xggTQMIIE
# zAIBATCBkDB5MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
# A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSMw
# IQYDVQQDExpNaWNyb3NvZnQgQ29kZSBTaWduaW5nIFBDQQITMwAAAUCWqe5wVv7M
# BwABAAABQDAJBgUrDgMCGgUAoIHpMBkGCSqGSIb3DQEJAzEMBgorBgEEAYI3AgEE
# MBwGCisGAQQBgjcCAQsxDjAMBgorBgEEAYI3AgEVMCMGCSqGSIb3DQEJBDEWBBTL
# Srm1s7hyf6DPl27Ey0/8Je8caDCBiAYKKwYBBAGCNwIBDDF6MHigWoBYAFAAdQBi
# AGwAaQBzAGgALQBVAHAAZwByAGEAZABlAGQAUwBlAHIAdgBpAGMAZQBGAGEAYgBy
# AGkAYwBBAHAAcABsAGkAYwBhAHQAaQBvAG4ALgBwAHMAMaEagBhodHRwOi8vd3d3
# Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEBBQAEggEAjojTxzQcEzu8LzAPVFl9
# ZHkfdvVWXfHUVE5qknBhI5WWcCzQTsE49+PTCBjQXeFxz6sKrM9U2T++K2cDOfbD
# O84eJlNjLxOcX76cbSjdix4d5CzJG/s0WkHVaaY/eAlNVWdVRPIxe1R0dA6cyZyC
# GMXHZL+Y1iZiAhAhxAbWwNmRl9YeH+Pni8kNmlRauTtgNmu9zvNP7bkd4fZMTakN
# oF1CTrn8QsCNbR+pZfiBoEtFPvsjDkWexrgRMCuPHbvOKAANbHk5/G3pFsba1ntd
# mb+NZ8I0fsi7ZWo6UWtgJmJcTAudeiEAt//iwsjRT+bvmJDqhL9oaRwpLa8hO2BK
# d6GCAigwggIkBgkqhkiG9w0BCQYxggIVMIICEQIBATCBjjB3MQswCQYDVQQGEwJV
# UzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UE
# ChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSEwHwYDVQQDExhNaWNyb3NvZnQgVGlt
# ZS1TdGFtcCBQQ0ECEzMAAADGeIA2AXUaaXIAAAAAAMYwCQYFKw4DAhoFAKBdMBgG
# CSqGSIb3DQEJAzELBgkqhkiG9w0BBwEwHAYJKoZIhvcNAQkFMQ8XDTE2MTEyMjIz
# NTI1NVowIwYJKoZIhvcNAQkEMRYEFLZ0t+QL+Vf9asEBe2xtJgNtYKjzMA0GCSqG
# SIb3DQEBBQUABIIBACXqVRL1CEARmVZB/ipp/lB/jGFkUKZ/R60M5GhNDf11I64x
# PLiD8hc2FSdaL7ohetaKR97807qvppExpFm9kdHFLorLdKN+rAoYuS2WIoocDM/4
# ALu/v/4CUa7wVCOlmG++/zdyQG4qb2AXCZxWBmd/abGYSMh3S52bATcHXtvFsBj0
# ZTvCzy0Rq7yEmNzm1drLVS94eTj3U0syBXslo+xBynLqtVbDR1HuKiOyEar+sIry
# XjOS0CEj7AqSd1CF4K7HCEdwFvfccTebxQRdBFRYIRAQntZfwNCE4fcO5WOStHfI
# rEg8nFiKIb00K7jKmCf3A4b9BTSC2rJ6p8uGiP0=
# SIG # End signature block
