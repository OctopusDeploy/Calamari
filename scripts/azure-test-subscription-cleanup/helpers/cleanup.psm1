# Mocked functions that don't exist outside of Octopus must be defined first.

Function New-OctopusArtifact {
    Write-Host "Mocked function has been hit"
    return $null
}