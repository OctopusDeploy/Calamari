function IsDockerAvailable() {
    $command = $(Get-Command 'docker' -ErrorAction SilentlyContinue)
    if ($command -eq $null) {
        Write-Host 'docker command not available'
        return $false
    }

    try {
        & docker ps 1>$null 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host 'Docker was unable to connect to a docker service'
            return $false
        }
    }
    catch {
        Write-Host 'Unable to connect to docker service'
        return $false
    }

    return $LASTEXITCODE -eq 0
}


$dockerUsername=$OctopusParameters["DockerUsername"]
$dockerPassword=$OctopusParameters["DockerPassword"]
$feedUri=$OctopusParameters["FeedUri"]

if($(IsDockerAvailable) -eq $false) {
    Write-Error "You will need docker installed and running to pull docker images"
    exit 1;
}

Write-Verbose $(docker -v)

if (-not ([string]::IsNullOrEmpty($dockerUsername))) {
    # docker 17.07 throws a warning to stderr if you use the --password param
    $dockerVersion = & docker version --format '{{.Client.Version}}'
    $parsedVersion = [Version]($dockerVersion -split '-')[0]
    $dockerNeedsPasswordViaStdIn = (($parsedVersion.Major -gt 17) -or (($parsedVersion.Major -eq 17) -and ($parsedVersion.Minor -gt 6)))
    if ($dockerNeedsPasswordViaStdIn) {
        echo $dockerPassword | cmd /c "docker login --username $dockerUsername --password-stdin $feedUri 2>&1"
    } else {
        cmd /c "docker login --username $dockerUsername --password $dockerPassword $feedUri 2>&1"
    }

    if(!$?)
    {
        Write-Error "Login Failed"
        exit 1
    }
}