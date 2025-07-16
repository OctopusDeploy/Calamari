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
##   OctopusFabricCertificateFindValueOverride               // The type of FindValue for searching the certificate in the Azure certificate store (use this if you specify a FindType different to 'FindByThumbprint' and do NOT wish to use the client certificate thumbprint value)
##   OctopusFabricCertificateStoreLocation                   // The certificate store location (should be 'LocalMachine' by default)
##   OctopusFabricCertificateStoreName                       // The certificate store name (should be 'MY' by default)
##   OctopusFabricAadToken                                   // The AzureAD token for authentication

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

function ValidationMessageForClientCertificateParameters() {
    if (!$OctopusFabricServerCertThumbprint) {
        return "Failed to find a value for the server certificate."
    }
    if (!$OctopusFabricClientCertThumbprint) {
        return "Failed to find a value for the client certificate."
    }
    return $null
}

function ValidationMessageForAzureADParameters() {
    if (!$OctopusFabricServerCertThumbprint) {
        return "Failed to find a value for the server certificate."
    }

    return $null
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
            Write-Error $validationMsg
            Exit
        }

        $ClusterConnectionParameters["ServerCertThumbprint"] = $OctopusFabricServerCertThumbprint
        $ClusterConnectionParameters["X509Credential"] = $true
        $ClusterConnectionParameters["StoreLocation"] = $OctopusFabricCertificateStoreLocation
        $ClusterConnectionParameters["StoreName"] = $OctopusFabricCertificateStoreName
        $ClusterConnectionParameters["FindType"] = $OctopusFabricCertificateFindType

        if ($OctopusFabricCertificateFindValueOverride) {
            $ClusterConnectionParameters["FindValue"] = $OctopusFabricCertificateFindValueOverride
        } Else {
            $ClusterConnectionParameters["FindValue"] = $OctopusFabricClientCertThumbprint
        }

    } ElseIf ($OctopusFabricSecurityMode -eq "SecureAzureAD") {

        # Secure Azure AD
        Write-Verbose "Loading connection parameters for the 'Azure AD' security mode."

        $validationMsg = ValidationMessageForAzureADParameters
        if ($validationMsg) {
            Write-Error $validationMsg
            Exit
        }

        $AccessToken = $OctopusFabricAadToken
        if (!$AccessToken)
        {
            Write-Error "No access token could be found for Service Fabric to connect with."
            Exit
        }

        $ClusterConnectionParameters["ServerCertThumbprint"] = $OctopusFabricServerCertThumbprint
        $ClusterConnectionParameters["AzureActiveDirectory"] = $true
        $ClusterConnectionParameters["SecurityToken"] = $AccessToken

    } ElseIf ($OctopusFabricSecurityMode -eq "SecureAD") {

        # Secure AD
        Write-Verbose "Connecting to Service Fabric using AD security."

        $ClusterConnectionParameters["WindowsCredential"] = $true

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

if ([string]::IsNullOrEmpty($OctopusFabricTargetScriptParameters)) {
    Write-Verbose "Invoking target script '$OctopusFabricTargetScript'."
} else {
    Write-Verbose "Invoking target script '$OctopusFabricTargetScript' with parameters '$OctopusFabricTargetScriptParameters'."
}
Invoke-Expression ". `"$OctopusFabricTargetScript`" $OctopusFabricTargetScriptParameters"
