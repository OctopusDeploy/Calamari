## Octopus Azure Service Fabric Context script, version 1.0
## --------------------------------------------------------------------------------------
## 
## This script is used to establish a connection to the Azure Service Fabric cluster
##
## The script is passed the following parameters. 
##
##   OctopusFabricTargetScript
##   OctopusFabricTargetScriptParameters
##   OctopusFabricConnectionEndpoint                         // The connection endpoint
##   OctopusFabricSecurityMode                               // The security mode used to connect to the cluster
##   OctopusFabricServerCertThumbprint                       // The server certificate thumbprint
##   OctopusFabricClientCertThumbprint                       // The client certificate thumbprint
##   OctopusFabricCertificateFindType                        // The certificate lookup type (should be 'FindByThumbprint' by default)
##   OctopusFabricCertificateStoreLocation                   // The certificate store location (should be 'LocalMachine' by default)
##   OctopusFabricCertificateStoreName                       // The certificate store name (should be 'MY' by default)
##   OctopusFabricAadClientId                                // The client ID for AAD auth
##   OctopusFabricAadClientSecret                            // The client secret for AAD auth
##   OctopusFabricAadEnvironment                             // The azure environment for AAD auth (should be 'AzureCloud' by default)
##   OctopusFabricAadResourceId                             // The resource URL for AAD auth
##   OctopusFabricAadTenantId                                // The tenant ID for AAD auth
##   OctopusFabricActiveDirectoryLibraryPath                 // The path to Microsoft.IdentityModel.Clients.ActiveDirectory.dll

$ErrorActionPreference = "Stop"

#Write-Verbose "TODO: markse - remove this logging"
#Write-Verbose "OctopusFabricTargetScript = $($OctopusFabricTargetScript)"
#Write-Verbose "OctopusFabricTargetScriptParameters = $($OctopusFabricTargetScriptParameters)"
#Write-Verbose "OctopusFabricConnectionEndpoint = $($OctopusFabricConnectionEndpoint)"
#Write-Verbose "OctopusFabricSecurityMode = $($OctopusFabricSecurityMode)"
#Write-Verbose "OctopusFabricServerCertThumbprint = $($OctopusFabricServerCertThumbprint)"
#Write-Verbose "OctopusFabricClientCertThumbprint = $($OctopusFabricClientCertThumbprint)"
#Write-Verbose "OctopusFabricCertificateFindType = $($OctopusFabricCertificateFindType)"
#Write-Verbose "OctopusFabricCertificateStoreLocation = $($OctopusFabricCertificateStoreLocation)"
#Write-Verbose "OctopusFabricCertificateStoreName = $($OctopusFabricCertificateStoreName)"
#Write-Verbose "OctopusFabricAadClientId = $($OctopusFabricAadClientId)"
#Write-Verbose "OctopusFabricAadClientSecret = $($OctopusFabricAadClientSecret)"
#Write-Verbose "OctopusFabricAadEnvironment = $($OctopusFabricAadEnvironment)"
#Write-Verbose "OctopusFabricAadResourceId = $($OctopusFabricAadResourceId)"
#Write-Verbose "OctopusFabricAadTenantId = $($OctopusFabricAadTenantId)"
#Write-Verbose "OctopusFabricActiveDirectoryLibraryPath = $($OctopusFabricActiveDirectoryLibraryPath)"

# We need these PS modules for the AzureAD security mode (not available in SF SDK).
if ([System.Convert]::ToBoolean($OctopusUseBundledAzureModules)) {
    # Add bundled Azure PS modules to PSModulePath
    $StorageModulePath = Join-Path "$OctopusAzureModulePath" -ChildPath "Storage"
    $ServiceManagementModulePath = Join-Path "$OctopusAzureModulePath" -ChildPath "ServiceManagement"
    $ResourceManagerModulePath = Join-Path "$OctopusAzureModulePath" -ChildPath "ResourceManager" | Join-Path -ChildPath "AzureResourceManager"
    Write-Verbose "Adding bundled Azure PowerShell modules to PSModulePath"
    $env:PSModulePath = $ResourceManagerModulePath + ";" + $ServiceManagementModulePath + ";" + $StorageModulePath + ";" + $env:PSModulePath
}

function Execute-WithRetry([ScriptBlock] $command) {
    $attemptCount = 0
    $operationIncomplete = $true
    $sleepBetweenFailures = 5
    $maxFailures = 5

    while ($operationIncomplete -and $attemptCount -lt $maxFailures) {
        $attemptCount = ($attemptCount + 1)

        if ($attemptCount -ge 2) {
            Write-Host "Waiting for $sleepBetweenFailures seconds before retrying..."
            Start-Sleep -s $sleepBetweenFailures
            Write-Host "Retrying..."
        }

        try {
            & $command

            $operationIncomplete = $false
        } catch [System.Exception] {
            if ($attemptCount -lt ($maxFailures)) {
                Write-Host ("Attempt $attemptCount of $maxFailures failed: " + $_.Exception.Message)
            } else {
                throw
            }
        }
    }
}

function ValidationMessageForClientCertificateParameters() {
    if (!$OctopusFabricClientCertThumbprint) {
        return "Failed to find a value for the client certificate."
    }
    return $null
}

function ValidationMessageForAzureADParameters() {
    if (!$OctopusFabricAadEnvironment) {
        return "Failed to find a value for the Azure environment."
    }
    if (!$OctopusFabricAadClientId) {
        return "Failed to find a value for the client ID."
    }
    if (!$OctopusFabricAadClientSecret) {
        return "Failed to find a value for the client secret."
    }
    if (!$OctopusFabricAadResourceId) {
        return "Failed to find a value for the resource URL."
    }
    if (!$OctopusFabricAadTenantId) {
        return "Failed to find a value for the tenant ID."
    }
    return $null
}

function GetAzureADAccessToken() {
    # Ensure we can load the ActiveDirectory lib and add it to our PowerShell session.
    Try
    {
        $FilePath = Join-Path $OctopusFabricActiveDirectoryLibraryPath "Microsoft.IdentityModel.Clients.ActiveDirectory.dll"
        Add-Type -Path $FilePath
    }
    Catch
    {
        Write-Error "Unable to load the Microsoft.IdentityModel.Clients.ActiveDirectory.dll. Please ensure this library file exists at $($OctopusFabricActiveDirectoryLibraryPath)."
        Exit
    }

    # Get the AD Authority URL based on the Azure environment (this call uses Azure PS modules, not SF SDK).
    $AzureEnvironment = Get-AzureRmEnvironment -Name $OctopusFabricAadEnvironment
    if (!$AzureEnvironment)
    {
        Write-Error "No Azure environment could be matched given the name $OctopusFabricAadEnvironment."
        Exit
    }
    $AuthorityUrl = [System.IO.Path]::Combine($AzureEnvironment.ActiveDirectoryAuthority, $OctopusFabricAadTenantId)
    $AuthorityUrl = $AuthorityUrl.Replace('\', '/')
    Write-Verbose "Using ActiveDirectoryAuthority $($AuthorityUrl)."
        
    $UserCred = New-Object Microsoft.IdentityModel.Clients.ActiveDirectory.ClientCredential $OctopusFabricAadClientId, $OctopusFabricAadClientSecret
    $AuthenticationContext = New-Object Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext -ArgumentList $AuthorityUrl, $false
    $AccessToken = $AuthenticationContext.AcquireToken($OctopusFabricAadResourceId, $UserCred).AccessToken
    return $AccessToken
}

Execute-WithRetry {

    # Prepare a dictionary of connection parameters that we'll use to connect below.
    $ClusterConnectionParameters = @{}
    $ClusterConnectionParameters["ConnectionEndpoint"] = $OctopusFabricConnectionEndpoint

    If ($OctopusFabricSecurityMode -eq "SecureClientCertificate") {

        # Secure client certificate
        Write-Verbose "Loading connection parameters for the 'Client Certificate' security mode."

        $validationMsg = ValidationMessageForClientCertificateParameters
        if ($validationMsg) {
            Write-Warning $validationMsg
            Exit
        }

        $ClusterConnectionParameters["ServerCertThumbprint"] = $OctopusFabricServerCertThumbprint
        $ClusterConnectionParameters["X509Credential"] = $true
        $ClusterConnectionParameters["StoreLocation"] = $OctopusFabricCertificateStoreLocation
        $ClusterConnectionParameters["StoreName"] = $OctopusFabricCertificateStoreName
        $ClusterConnectionParameters["FindType"] = $OctopusFabricCertificateFindType
        $ClusterConnectionParameters["FindValue"] = $OctopusFabricClientCertThumbprint

    } ElseIf ($OctopusFabricSecurityMode -eq "SecureAzureAD") {

        # Secure Azure AD
        Write-Verbose "Loading connection parameters for the 'Azure AD' security mode."

        $validationMsg = ValidationMessageForAzureADParameters
        if ($validationMsg) {
            Write-Warning $validationMsg
            Exit
        }

        $AccessToken = GetAzureADAccessToken
        if (!$AccessToken)
        {
            Write-Error "No access token could be found for Service Fabric to connect with."
            Exit
        }
        
        $ClusterConnectionParameters["ServerCertThumbprint"] = $OctopusFabricServerCertThumbprint
        $ClusterConnectionParameters["AzureActiveDirectory"] = $true
        $ClusterConnectionParameters["SecurityToken"] = $AccessToken

    } Else {
        # Unsecure
        Write-Verbose "Connecting to Service Fabric unsecurely."
    }

    try
    {
        Write-Verbose "Authenticating with Service Fabric."

        $EnvironmentNewLine = [Environment]::NewLine
        $ClusterConnectionParametersText = ($ClusterConnectionParameters.GetEnumerator() | % { "$($_.Key)=$($_.Value)" }) -join "  $($EnvironmentNewLine)"
        Write-Verbose "Using ConnectionParameters:$($EnvironmentNewLine)$($ClusterConnectionParametersText)"

        [void](Connect-ServiceFabricCluster @ClusterConnectionParameters)

        # http://stackoverflow.com/questions/35711540/how-do-i-deploy-service-fabric-application-from-vsts-release-pipeline
        # When the Connect-ServiceFabricCluster function is called, a local $clusterConnection variable is set after the call to Connect-ServiceFabricCluster. You can see that using Get-Variable.
        # Unfortunately there is logic in some of the SDK scripts that expect that variable to be set but because they run in a different scope, that local variable isn't available.
        # It works in Visual Studio because the Deploy-FabricApplication.ps1 script is called using dot source notation, which puts the $clusterConnection variable in the current scope.
        # I'm not sure if there is a way to use dot sourcing when running a script though the release pipeline but you could, as a workaround, make the $clusterConnection variable global right after it's been set via the Connect-ServiceFabricCluster call.
        $global:clusterConnection = $clusterConnection
    }
    catch [System.Fabric.FabricObjectClosedException]
    {
        Write-Warning "Service Fabric cluster may not be connected."
        throw
    }
}

Write-Verbose "Invoking target script $OctopusFabricTargetScript with $OctopusFabricTargetScriptParameters parameters."
Invoke-Expression ". $OctopusFabricTargetScript $OctopusFabricTargetScriptParameters"