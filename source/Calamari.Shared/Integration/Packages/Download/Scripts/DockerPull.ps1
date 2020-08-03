$IMAGE=$OctopusParameters["Image"]
$dockerUsername=$OctopusParameters["DockerUsername"]
$dockerPassword=$OctopusParameters["DockerPassword"]
$feedUri=$OctopusParameters["FeedUri"]

try {
  Write-Verbose $(Get-Process 'com.docker.proxy' -ErrorAction Stop)
}
Catch [System.Exception] {
  Write-Error "You will need docker installed and running to pull docker images"
  Write-Error $_
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
