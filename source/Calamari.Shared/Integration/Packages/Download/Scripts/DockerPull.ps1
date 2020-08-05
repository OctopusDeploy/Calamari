function IsDockerAvailable() {
    $service = $(Get-Service -Name 'docker' -ErrorAction SilentlyContinue)
    $serviceRunning = ($service -ne $null -and $service.Status -eq 'Running')
    $process = $(Get-Process 'com.docker.proxy' -ErrorAction SilentlyContinue)
    $command = $(Get-Command 'docker' -ErrorAction SilentlyContinue)
    $dockerAvailable = $command -ne $null -and ($process -ne $null -or $serviceRunning)
    return $dockerAvailable
}

$IMAGE=$OctopusParameters["Image"]
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

Write-Verbose "docker pull $IMAGE"
docker pull $IMAGE
