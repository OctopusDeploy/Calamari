## Octopus Azure Service Fabric Context script, version 1.0
## --------------------------------------------------------------------------------------
## 
## This script is used to establish a connection to the Azure Service Fabric cluster
##
## The script is passed the following parameters. 
##
##   OctopusAzureTargetScript
##   OctopusAzureTargetScriptParameters
##   OctopusFabricConnectionEndpoint                         // The connection endpoint
##   OctopusFabricIsSecure                                   // Indicates whether the fabric connection is secured by an X509 cert
##   OctopusFabricServerCertThumbprint                       // The server certificate thumbprint
##   OctopusFabricCertificateFindType                        // The client certificate lookup type
##   OctopusFabricCertificateFindValue                       // The client certificate thumbprint
##   OctopusFabricCertificateStoreLocation                   // The cert store location
##   OctopusFabricCertificateStoreName                       // The cert store name

$ErrorActionPreference = "Stop"

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

Execute-WithRetry{

	# Prepare a dictionary of connection parameters that we'll use to connect below.
	$ClusterConnectionParameters = @{}
	$ClusterConnectionParameters["ConnectionEndpoint"] = $OctopusFabricConnectionEndpoint

    If ([System.Convert]::ToBoolean($OctopusFabricIsSecure)) {
        # Secure (client certificate)
        Write-Verbose "Connect to Service Fabric securely (client certificate)"
		$ClusterConnectionParameters["ServerCertThumbprint"] = $OctopusFabricServerCertThumbprint
		$ClusterConnectionParameters["X509Credential"] = $true
		$ClusterConnectionParameters["StoreLocation"] = $OctopusFabricCertificateStoreLocation
		$ClusterConnectionParameters["StoreName"] = $OctopusFabricCertificateStoreName
		$ClusterConnectionParameters["FindType"] = $OctopusFabricCertificateFindType
		$ClusterConnectionParameters["FindValue"] = $OctopusFabricCertificateFindValue
    } Else {
        # Unsecure
        Write-Verbose "Connect to Service Fabric unsecurely"
    }

    try
    {
        Write-Verbose "Authenticating with Service Fabric"
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

Write-Verbose "Invoking target script $OctopusAzureTargetScript with $OctopusAzureTargetScriptParameters parameters"
Invoke-Expression ". $OctopusAzureTargetScript $OctopusAzureTargetScriptParameters"