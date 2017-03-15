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
##   OctopusFabricAadEnvironment                             // The azure environment for AAD auth (should be 'AzureCloud' by default)
##   OctopusFabricAadResourceUrl                             // The resource URL for AAD auth
##   OctopusFabricAadTenantId                                // The tenant ID for AAD auth
##   OctopusFabricActiveDirectoryLibraryPath                 // The path to Microsoft.IdentityModel.Clients.ActiveDirectory.dll

$ErrorActionPreference = "Stop"

Write-Verbose "TODO: markse - remove this logging"
Write-Verbose "OctopusFabricTargetScript = $($OctopusFabricTargetScript)"
Write-Verbose "OctopusFabricTargetScriptParameters = $($OctopusFabricTargetScriptParameters)"
Write-Verbose "OctopusFabricConnectionEndpoint = $($OctopusFabricConnectionEndpoint)"
Write-Verbose "OctopusFabricSecurityMode = $($OctopusFabricSecurityMode)"
Write-Verbose "OctopusFabricServerCertThumbprint = $($OctopusFabricServerCertThumbprint)"
Write-Verbose "OctopusFabricClientCertThumbprint = $($OctopusFabricClientCertThumbprint)"
Write-Verbose "OctopusFabricCertificateFindType = $($OctopusFabricCertificateFindType)"
Write-Verbose "OctopusFabricCertificateStoreLocation = $($OctopusFabricCertificateStoreLocation)"
Write-Verbose "OctopusFabricCertificateStoreName = $($OctopusFabricCertificateStoreName)"
Write-Verbose "OctopusFabricAadClientId = $($OctopusFabricAadClientId)"
Write-Verbose "OctopusFabricAadEnvironment = $($OctopusFabricAadEnvironment)"
Write-Verbose "OctopusFabricAadResourceUrl = $($OctopusFabricAadResourceUrl)"
Write-Verbose "OctopusFabricAadTenantId = $($OctopusFabricAadTenantId)"
Write-Verbose "OctopusFabricActiveDirectoryLibraryPath = $($OctopusFabricActiveDirectoryLibraryPath)"

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

# We need these PS modules for the AzureAD security mode (not available in SF SDK).
if ([System.Convert]::ToBoolean($OctopusUseBundledAzureModules)) {
    # Add bundled Azure PS modules to PSModulePath
    $StorageModulePath = Join-Path "$OctopusAzureModulePath" -ChildPath "Storage"
    $ServiceManagementModulePath = Join-Path "$OctopusAzureModulePath" -ChildPath "ServiceManagement"
    $ResourceManagerModulePath = Join-Path "$OctopusAzureModulePath" -ChildPath "ResourceManager" | Join-Path -ChildPath "AzureResourceManager"
    Write-Verbose "Adding bundled Azure PowerShell modules to PSModulePath"
    $env:PSModulePath = $ResourceManagerModulePath + ";" + $ServiceManagementModulePath + ";" + $StorageModulePath + ";" + $env:PSModulePath
}

Execute-WithRetry{

	# Prepare a dictionary of connection parameters that we'll use to connect below.
	$ClusterConnectionParameters = @{}
	$ClusterConnectionParameters["ConnectionEndpoint"] = $OctopusFabricConnectionEndpoint

    If ($OctopusFabricSecurityMode -eq "SecureClientCertificate") {

		# Secure client certificate
		Write-Verbose "Loading connection parameters for the 'client certificate' security mode."

		if (!$OctopusFabricClientCertThumbprint) {
			Write-Warning "Failed to find a value for the client certificate."
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
		
		if (!$OctopusFabricAadEnvironment) {
			Write-Warning "Failed to find a value for the Azure environment."
			Exit
		}
		
		if (!$OctopusFabricAadClientId) {
			Write-Warning "Failed to find a value for the client ID."
			Exit
		}
		
		if (!$OctopusFabricAadResourceUrl) {
			Write-Warning "Failed to find a value for the resource URL."
			Exit
		}
		
		if (!$OctopusFabricAadTenantId) {
			Write-Warning "Failed to find a value for the tenant ID."
			Exit
		}

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

		# Get the AD Authority URL based on our environment (uses Azure PS modules, not SF SDK).
		$AzureEnvironment = Get-AzureRmEnvironment -Name $OctopusFabricAadEnvironment
        if (!$AzureEnvironment)
        {
            Write-Error "No Azure environment could be matched given the name $OctopusFabricAadEnvironment."
            Exit
        }
		#TODO: markse - use something instead of string concat here, feels messy/dangerous.
		$AuthorityUrl = "$($AzureEnvironment.ActiveDirectoryAuthority)$($OctopusFabricAadTenantId)"
		Write-Verbose "Using ActiveDirectoryAuthority $($AuthorityUrl)."
		
		$UserCred = New-Object Microsoft.IdentityModel.Clients.ActiveDirectory.ClientCredential $OctopusFabricAadClientId, "DYBhrmVbf1PeRBclOMi6y6z9d8yWQMg/vS/1S8Fn8+w="
		$AuthenticationContext = New-Object Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext -ArgumentList $AuthorityUrl, $false
		$AccessToken = $AuthenticationContext.AcquireToken($OctopusFabricAadResourceUrl, $UserCred).AccessToken
		if (!$AccessToken)
        {
            Write-Error "No access token could be found for Service Fabric to connect with."
            Exit
        }
		$ClusterConnectionParameters["AzureActiveDirectory"] = $true
		$ClusterConnectionParameters["SecurityToken"] = $AccessToken

    } Else {
        # Unsecure
        Write-Verbose "Connecting to Service Fabric unsecurely."
    }

    try
    {
        Write-Verbose "Authenticating with Service Fabric."
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