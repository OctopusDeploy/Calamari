function Get-NodesJson() {
    $nodes = kubectl get nodes -o=custom-columns=NAME:.metadata.name --all-namespaces | Select-Object -Skip 1
    $nodes = $nodes | ForEach-Object { """$_""" }
    $nodes = $nodes -join ","

    $nodesJson = @"
    [$nodes]
"@

    return $nodesJson
}

function Get-Parameters() {

    param (
        [string]$DeploymentTargetId,
        [string]$Metadata
    )

    $parameters = ""
    $tempParameter = Convert-ToServiceMessageParameter -name "deploymentTargetId" -value $DeploymentTargetId
    $parameters = $parameters, $tempParameter -join ' '
    $tempParameter = Convert-ToServiceMessageParameter -name "metadata" -value $Metadata
    $parameters = $parameters, $tempParameter -join ' '

    return $parameters
}

Write-Host kubectl version to test connectivity
kubectl version --short | Out-Default

# Only proceed to gathering metadata if we successfully talked to the cluster via the version command
if ($global:LASTEXITCODE -eq 0) {
    
    $canGetNodes = kubectl auth can-i get nodes --all-namespaces

    if ($canGetNodes -eq "yes") {

        $metadata = @"
    {
        "type": "kubernetes",
        "thumbprint": "%THUMBPRINT%",
        "metadata": {
            "nodes": $(Get-NodesJson)
        }
    }
"@

        $parameters = Get-Parameters -DeploymentTargetId "%DEPLOYMENTTARGETID%" -Metadata $metadata

        Write-Host "##octopus[set-deploymenttargetmetadata $($parameters)]"
    }
    else {
        $metadata = @"
    {
        "type": "kubernetes",
        "thumbprint": "%THUMBPRINT%",
        "metadata": {
            "INSUFFICIENTPRIVILEGES": "Insufficient privileges to retrieve deployment target metadata"
        }
    }
"@

        $parameters = Get-Parameters -DeploymentTargetId "%DEPLOYMENTTARGETID%" -Metadata $metadata

        Write-Host "##octopus[set-deploymenttargetmetadata $($parameters)]"
    }

    # Force a successful exit, even if we failed to retrieve cluster metadata
    $global:LASTEXITCODE = 0
    exit 0
}

